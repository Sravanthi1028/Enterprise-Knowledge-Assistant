
using EnterpriseKnowledge.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseKnowledge.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EmbeddingController : ControllerBase
    {
        private readonly EmbeddingService _embeddingService;

        public EmbeddingController(EmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;
        }

        // Generate embedding for document text
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateEmbedding(
            [FromBody] EmbeddingRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new
                {
                    message = "Text cannot be empty."
                });
            }

            var embedding =
                await _embeddingService.GenerateEmbeddingAsync(
                    request.Text,
                    "RETRIEVAL_DOCUMENT"
                );

            return Ok(new
            {
                message = "Document embedding generated successfully.",
                dimensions = embedding.Count,
                embedding = embedding
            });
        }

        // Generate embedding for user query
        [HttpPost("query")]
        public async Task<IActionResult> GenerateQueryEmbedding(
            [FromBody] EmbeddingRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new
                {
                    message = "Query text cannot be empty."
                });
            }

            var embedding =
                await _embeddingService.GenerateEmbeddingAsync(
                    request.Text,
                    "RETRIEVAL_QUERY"
                );

            return Ok(new
            {
                message = "Query embedding generated successfully.",
                dimensions = embedding.Count,
                embedding = embedding
            });
        }
    }

    public class EmbeddingRequest
    {
        public string Text { get; set; } = string.Empty;
    }
}
