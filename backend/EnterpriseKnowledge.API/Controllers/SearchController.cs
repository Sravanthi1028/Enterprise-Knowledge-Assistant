
using EnterpriseKnowledge.API.DTOs;
using EnterpriseKnowledge.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseKnowledge.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly DocumentSearchService _searchService;

        public SearchController(DocumentSearchService searchService)
        {
            _searchService = searchService;
        }

        // POST: api/Search
        // Keyword search
        [HttpPost]
        public async Task<IActionResult> SearchDocuments(
            [FromBody] DocumentSearchRequestDto request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new
                {
                    message = "Search query cannot be empty."
                });
            }

            var results = await _searchService.SearchDocumentsAsync(
                request.Query,
                request.DocumentId
            );

            return Ok(new
            {
                searchType = "Keyword Search",
                query = request.Query,
                totalResults = results.Count,
                results
            });
        }

        // POST: api/Search/semantic
        // Semantic search
        [HttpPost("semantic")]
        public async Task<IActionResult> SemanticSearch(
            [FromBody] DocumentSearchRequestDto request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new
                {
                    message = "Search query cannot be empty."
                });
            }

            var results = await _searchService.SearchSemanticAsync(
                request.Query,
                request.DocumentId
            );

            return Ok(new
            {
                searchType = "Semantic Search",
                query = request.Query,
                totalResults = results.Count,
                results
            });
        }

        // POST: api/Search/hybrid
        // Combined keyword and semantic search
        [HttpPost("hybrid")]
        public async Task<IActionResult> HybridSearch(
            [FromBody] DocumentSearchRequestDto request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new
                {
                    message = "Search query cannot be empty."
                });
            }

            var results = await _searchService.SearchHybridAsync(
                request.Query,
                request.DocumentId
            );

            return Ok(new
            {
                searchType = "Hybrid Search",
                query = request.Query,
                totalResults = results.Count,
                results
            });
        }
    }
}
