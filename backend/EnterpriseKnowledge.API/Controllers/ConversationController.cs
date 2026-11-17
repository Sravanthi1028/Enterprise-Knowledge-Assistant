
using EnterpriseKnowledge.API.Data;
using EnterpriseKnowledge.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EnterpriseKnowledge.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ConversationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConversationController(AppDbContext context)
        {
            _context = context;
        }

        // Get logged-in user's ID from JWT
        private int GetUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userId!);
        }

        // Create a new conversation
        [HttpPost("create")]
        public async Task<IActionResult> CreateConversation(
            [FromBody] CreateConversationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new
                {
                    message = "Conversation title is required."
                });
            }

            var conversation = new Conversation
            {
                UserId = GetUserId(),
                Title = request.Title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Conversation created successfully.",
                conversationId = conversation.ConversationId,
                title = conversation.Title
            });
        }

        // Get all conversations of logged-in user
        [HttpGet("my-conversations")]
        public async Task<IActionResult> GetMyConversations()
        {
            int userId = GetUserId();

            var conversations = await _context.Conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new
                {
                    c.ConversationId,
                    c.Title,
                    c.CreatedAt,
                    c.UpdatedAt
                })
                .ToListAsync();

            return Ok(conversations);
        }

        // Get messages of a conversation
        [HttpGet("{conversationId}/messages")]
        public async Task<IActionResult> GetMessages(int conversationId)
        {
            int userId = GetUserId();

            var conversationExists = await _context.Conversations
                .AnyAsync(c =>
                    c.ConversationId == conversationId &&
                    c.UserId == userId);

            if (!conversationExists)
            {
                return NotFound(new
                {
                    message = "Conversation not found."
                });
            }

            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.MessageId,
                    m.Role,
                    m.Content,
                    m.CreatedAt
                })
                .ToListAsync();

            return Ok(messages);
        }

        // Delete a conversation belonging to the logged-in user
        [HttpDelete("{conversationId}")]
        public async Task<IActionResult> DeleteConversation(int conversationId)
        {
            int userId = GetUserId();

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c =>
                    c.ConversationId == conversationId &&
                    c.UserId == userId);

            if (conversation == null)
            {
                return NotFound(new
                {
                    message = "Conversation not found."
                });
            }

            // Delete associated messages first
            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .ToListAsync();

            _context.ChatMessages.RemoveRange(messages);

            // Delete the conversation
            _context.Conversations.Remove(conversation);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Conversation and associated messages deleted successfully.",
                conversationId = conversationId
            });
        }
    }

    public class CreateConversationRequest
    {
        public string Title { get; set; } = string.Empty;
    }
}
