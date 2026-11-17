
using EnterpriseKnowledge.API.Data;
using EnterpriseKnowledge.API.Models;
using EnterpriseKnowledge.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace EnterpriseKnowledge.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RAGController : ControllerBase
    {
        private readonly RAGService _ragService;
        private readonly AppDbContext _context;

        public RAGController(
            RAGService ragService,
            AppDbContext context)
        {
            _ragService = ragService;
            _context = context;
        }

        private int GetUserId()
        {
            var userId = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userId, out int id))
            {
                throw new UnauthorizedAccessException(
                    "Invalid user authentication.");
            }

            return id;
        }

        // 1. Retrieve document context
        [HttpPost("context")]
        public async Task<IActionResult> GetContext(
            [FromBody] RAGRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new
                {
                    message = "Question cannot be empty."
                });
            }

            var ragResult = await _ragService.BuildContextAsync(
                request.Query,
                request.DocumentId
            );

            return Ok(new
            {
                question = request.Query,
                documentId = request.DocumentId,
                retrievedContext = ragResult.Context,
                sources = ragResult.Sources
            });
        }

        // 2. Ask questions using RAG
        [HttpPost("ask")]
        public async Task<IActionResult> AskQuestion(
            [FromBody] RAGRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new
                {
                    message = "Question cannot be empty."
                });
            }

            if (!request.ConversationId.HasValue)
            {
                return BadRequest(new
                {
                    message = "Conversation ID is required."
                });
            }

            int userId = GetUserId();

            // Verify conversation ownership
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c =>
                    c.ConversationId == request.ConversationId.Value &&
                    c.UserId == userId);

            if (conversation == null)
            {
                return NotFound(new
                {
                    message = "Conversation not found."
                });
            }

            // 3. Retrieve the latest five previous messages
            var previousMessages = await _context.ChatMessages
                .Where(m =>
                    m.ConversationId == conversation.ConversationId)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.MessageId)
                .Take(5)
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.MessageId)
                .ToListAsync();

            // 4. Detect follow-up questions
            string[] followUpPhrases =
            {
                "explain further",
                "what about",
                "tell me more",
                "give more details",
                "elaborate"
            };

            bool containsFollowUpWord = Regex.IsMatch(
                request.Query,
                @"\b(it|its|they|them|this|that|these|those|more)\b",
                RegexOptions.IgnoreCase
            );

            bool containsFollowUpPhrase = followUpPhrases.Any(
                phrase => request.Query.Contains(
                    phrase,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            bool isFollowUp =
                previousMessages.Count > 0 &&
                (containsFollowUpWord || containsFollowUpPhrase);

            // 5. Retrieve optimized hybrid-search context
            var ragResult = await _ragService.BuildContextAsync(
                request.Query,
                request.DocumentId
            );

            // 6. Check whether the question asks for a quantity
            bool asksForQuantity = Regex.IsMatch(
                request.Query,
                @"\b(how many|how much|number of|quantity of)\b",
                RegexOptions.IgnoreCase
            );

            // Check whether retrieved documents contain a quantity
            bool containsQuantity = ragResult.Sources.Any(source =>
                Regex.IsMatch(
                    source.Content ?? "",
                    @"\b\d+(?:\.\d+)?\b|\b(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|fifteen|twenty|thirty|forty|fifty|sixty|ninety|hundred|thousand)\b",
                    RegexOptions.IgnoreCase
                )
            );

            // 7. Build a grounded response
            string answer;

            if (ragResult.Sources.Count == 0)
            {
                answer =
                    "I could not find relevant information in the uploaded " +
                    "documents to answer your question. Please try rephrasing " +
                    "your question or upload a relevant document.";
            }
            else if (asksForQuantity && !containsQuantity)
            {
                answer =
                    "The uploaded documents do not specify the quantity " +
                    "or number requested in your question. " +
                    "Although related information was found, " +
                    "I cannot confirm the requested number from the available sources.";
            }
            else
            {
                StringBuilder answerBuilder = new StringBuilder();

                answerBuilder.AppendLine(
                    "AI answer generation is temporarily unavailable. " +
                    "The following information was retrieved from your documents:"
                );

                answerBuilder.AppendLine();

                int sourceNumber = 1;

                foreach (var source in ragResult.Sources)
                {
                    if (string.IsNullOrWhiteSpace(source.Content))
                        continue;

                    answerBuilder.AppendLine(
                        $"[Source {sourceNumber}]"
                    );

                    answerBuilder.AppendLine(
                        $"Document: {source.Title}"
                    );

                    answerBuilder.AppendLine(
                        $"File: {source.FileName}"
                    );

                    answerBuilder.AppendLine(
                        $"Chunk ID: {source.ChunkId}"
                    );

                    answerBuilder.AppendLine(
                        $"Content: {source.Content}"
                    );

                    answerBuilder.AppendLine();

                    sourceNumber++;
                }

                answerBuilder.AppendLine(
                    "Note: This response contains retrieved document content only. " +
                    "It does not establish facts beyond the cited sources."
                );

                answer = answerBuilder.ToString();
            }

            // 8. Save user's question
            var userMessage = new ChatMessage
            {
                ConversationId = conversation.ConversationId,
                Role = "User",
                Content = request.Query,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(userMessage);

            // 9. Save assistant response
            var assistantMessage = new ChatMessage
            {
                ConversationId = conversation.ConversationId,
                Role = "Assistant",
                Content = answer,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(assistantMessage);

            conversation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // 10. Return response with citations and history
            return Ok(new
            {
                conversationId = conversation.ConversationId,
                question = request.Query,
                answer = answer,
                isFollowUp = isFollowUp,
                retrievedContext = ragResult.Context,
                sources = ragResult.Sources,

                previousMessages = previousMessages.Select(m => new
                {
                    m.Role,
                    m.Content
                })
            });
        }
    }

    // Request model
    public class RAGRequest
    {
        public string Query { get; set; } = string.Empty;

        public int? DocumentId { get; set; }

        public int? ConversationId { get; set; }
    }
}
