
using System.Text;
using EnterpriseKnowledge.API.Models;

namespace EnterpriseKnowledge.API.Services
{
    public class RAGService
    {
        private readonly DocumentSearchService _searchService;

        private const int MaxContextLength = 6000;

        public RAGService(DocumentSearchService searchService)
        {
            _searchService = searchService;
        }

        public async Task<(string Context, List<RAGSource> Sources)>
            BuildContextAsync(string query, int? documentId = null)
        {
            // Step 1: Retrieve relevant chunks using Hybrid Search
            var results = await _searchService.SearchHybridAsync(
                query,
                documentId
            );

            List<RAGSource> sources = new List<RAGSource>();

            StringBuilder context = new StringBuilder();

            if (results.Count == 0)
            {
                return ("No relevant information found.", sources);
            }

            // Step 2: Remove duplicate content
            var uniqueResults = results
                .GroupBy(r => r.Content.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            // Step 3: Build optimized context
            foreach (var result in uniqueResults)
            {
                string content = result.Content.Trim();

                if (string.IsNullOrWhiteSpace(content))
                    continue;

                string sourceText =
                    $"Document: {result.Title}\n" +
                    $"Content: {content}\n\n";

                // Step 4: Enforce context length limit
                if (context.Length + sourceText.Length > MaxContextLength)
                    break;

                context.Append(sourceText);

                // Step 5: Preserve source information
                sources.Add(new RAGSource
                {
                    DocumentId = result.DocumentId,
                    Title = result.Title,
                    FileName = result.FileName,
                    ChunkId = result.ChunkId,
                    ChunkIndex = result.ChunkIndex,
                    Content = content
                });
            }

            if (sources.Count == 0)
            {
                return ("No relevant information found.", sources);
            }

            return (context.ToString(), sources);
        }
    }
}
