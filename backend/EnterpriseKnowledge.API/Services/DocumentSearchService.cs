
using EnterpriseKnowledge.API.Data;
using EnterpriseKnowledge.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EnterpriseKnowledge.API.Services
{
    public class DocumentSearchService
    {
        private readonly AppDbContext _context;
        private readonly EmbeddingService _embeddingService;

        private const double SimilarityThreshold = 0.65;

        private static readonly HashSet<string> StopWords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "how", "can", "i", "the", "a", "an",
                "is", "are", "for", "to", "of", "in",
                "on", "and", "or", "what", "where",
                "when", "why", "who", "which", "do",
                "does", "did", "will", "would", "could",
                "should", "my", "me", "your", "you",
                "their", "there", "this", "that"
            };

        public DocumentSearchService(
            AppDbContext context,
            EmbeddingService embeddingService)
        {
            _context = context;
            _embeddingService = embeddingService;
        }

        // 1. Keyword Search
        public async Task<List<object>> SearchDocumentsAsync(
            string query, int? documentId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<object>();

            query = query.Trim();

            var searchTerms = query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var documentsQuery =
                from chunk in _context.DocumentChunks
                join document in _context.Documents
                on chunk.DocumentId equals document.DocumentId
                where document.ProcessingStatus == "Completed"
                select new
                {
                    document.DocumentId,
                    document.Title,
                    document.FileName,
                    chunk.ChunkId,
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.CreatedAt
                };

            if (documentId.HasValue)
            {
                documentsQuery = documentsQuery
                    .Where(result =>
                        result.DocumentId == documentId.Value);
            }

            var results = await documentsQuery.ToListAsync();

            var rankedResults = results
                .Select(result =>
                {
                    int score = 0;

                    string title = result.Title ?? "";
                    string content = result.Content ?? "";

                    if (title.Contains(
                        query, StringComparison.OrdinalIgnoreCase))
                        score += 10;

                    if (content.Contains(
                        query, StringComparison.OrdinalIgnoreCase))
                        score += 8;

                    foreach (var term in searchTerms)
                    {
                        if (ContainsWholeTerm(title, term))
                            score += 3;

                        if (ContainsWholeTerm(content, term))
                            score += 1;
                    }

                    return new
                    {
                        result.DocumentId,
                        result.Title,
                        result.FileName,
                        result.ChunkId,
                        result.ChunkIndex,
                        result.Content,
                        result.CreatedAt,
                        RelevanceScore = score
                    };
                })
                .Where(result => result.RelevanceScore > 0)
                .OrderByDescending(result => result.RelevanceScore)
                .ThenByDescending(result => result.CreatedAt)
                .Take(3)
                .ToList();

            return rankedResults
                .Select(result => (object)result)
                .ToList();
        }

        // 2. Semantic Search
        public async Task<List<object>> SearchSemanticAsync(
            string query, int? documentId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<object>();

            var queryEmbedding =
                await _embeddingService.GenerateEmbeddingAsync(
                    query,
                    "RETRIEVAL_QUERY"
                );

            var documentsQuery =
                from chunk in _context.DocumentChunks
                join document in _context.Documents
                on chunk.DocumentId equals document.DocumentId
                where document.ProcessingStatus == "Completed"
                    && chunk.Embedding != null
                    && chunk.Embedding != ""
                select new
                {
                    document.DocumentId,
                    document.Title,
                    document.FileName,
                    chunk.ChunkId,
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.Embedding,
                    chunk.CreatedAt
                };

            if (documentId.HasValue)
            {
                documentsQuery = documentsQuery
                    .Where(result =>
                        result.DocumentId == documentId.Value);
            }

            var chunks = await documentsQuery.ToListAsync();

            var rankedResults = chunks
                .Select(chunk =>
                {
                    try
                    {
                        var storedEmbedding =
                            JsonSerializer.Deserialize<List<float>>(
                                chunk.Embedding!
                            );

                        if (storedEmbedding == null ||
                            storedEmbedding.Count != queryEmbedding.Count)
                            return null;

                        double similarity =
                            CalculateCosineSimilarity(
                                queryEmbedding,
                                storedEmbedding
                            );

                        return new
                        {
                            chunk.DocumentId,
                            chunk.Title,
                            chunk.FileName,
                            chunk.ChunkId,
                            chunk.ChunkIndex,
                            chunk.Content,
                            chunk.CreatedAt,
                            SimilarityScore = similarity
                        };
                    }
                    catch (JsonException)
                    {
                        return null;
                    }
                })
                .Where(result =>
                    result != null &&
                    result.SimilarityScore >= SimilarityThreshold)
                .OrderByDescending(result => result!.SimilarityScore)
                .Take(3)
                .ToList();

            return rankedResults
                .Select(result => (object)result!)
                .ToList();
        }

        // 3. Hybrid Search: Keyword + Semantic
        public async Task<List<HybridSearchResult>> SearchHybridAsync(
            string query, int? documentId = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<HybridSearchResult>();

            // Generate query embedding
            var queryEmbedding =
                await _embeddingService.GenerateEmbeddingAsync(
                    query,
                    "RETRIEVAL_QUERY"
                );

            // Retrieve completed document chunks
            var documentsQuery =
                from chunk in _context.DocumentChunks
                join document in _context.Documents
                on chunk.DocumentId equals document.DocumentId
                where document.ProcessingStatus == "Completed"
                select new
                {
                    document.DocumentId,
                    document.Title,
                    document.FileName,
                    chunk.ChunkId,
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.Embedding
                };

            if (documentId.HasValue)
            {
                documentsQuery = documentsQuery
                    .Where(result =>
                        result.DocumentId == documentId.Value);
            }

            var chunks = await documentsQuery.ToListAsync();

            // Remove common stop words
            var searchTerms = query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(term => term.Trim().ToLowerInvariant())
                .Where(term =>
                    term.Length >= 3 &&
                    !StopWords.Contains(term))
                .Distinct()
                .ToArray();

            var results = new List<HybridSearchResult>();

            foreach (var chunk in chunks)
            {
                int keywordScore = 0;

                string title = chunk.Title ?? "";
                string content = chunk.Content ?? "";

                // Exact phrase matching
                if (title.Contains(
                    query, StringComparison.OrdinalIgnoreCase))
                    keywordScore += 10;

                if (content.Contains(
                    query, StringComparison.OrdinalIgnoreCase))
                    keywordScore += 8;

                // Whole-word keyword matching
                foreach (var term in searchTerms)
                {
                    if (ContainsWholeTerm(title, term))
                        keywordScore += 3;

                    if (ContainsWholeTerm(content, term))
                        keywordScore += 1;
                }

                // Calculate semantic score
                double semanticScore = 0;

                if (!string.IsNullOrWhiteSpace(chunk.Embedding))
                {
                    try
                    {
                        var storedEmbedding =
                            JsonSerializer.Deserialize<List<float>>(
                                chunk.Embedding
                            );

                        if (storedEmbedding != null &&
                            storedEmbedding.Count == queryEmbedding.Count)
                        {
                            semanticScore =
                                CalculateCosineSimilarity(
                                    queryEmbedding,
                                    storedEmbedding
                                );
                        }
                    }
                    catch (JsonException)
                    {
                        semanticScore = 0;
                    }
                }

                results.Add(new HybridSearchResult
                {
                    DocumentId = chunk.DocumentId,
                    Title = title,
                    FileName = chunk.FileName ?? "",
                    ChunkId = chunk.ChunkId,
                    ChunkIndex = chunk.ChunkIndex,
                    Content = content,
                    SemanticScore = semanticScore,
                    KeywordScore = keywordScore
                });
            }

            // Find maximum keyword score
            int maxKeywordScore = results.Count > 0
                ? results.Max(r => r.KeywordScore)
                : 0;

            // Calculate combined ranking score
            foreach (var result in results)
            {
                double normalizedKeywordScore =
                    maxKeywordScore > 0
                        ? (double)result.KeywordScore / maxKeywordScore
                        : 0;

                result.CombinedScore =
                    (0.7 * result.SemanticScore) +
                    (0.3 * normalizedKeywordScore);
            }

            // Filter irrelevant results, remove duplicates and rank
            return results
                .Where(r =>
                    r.SemanticScore >= SimilarityThreshold ||
                    r.KeywordScore > 0)
                .GroupBy(r => r.ChunkId)
                .Select(group => group.First())
                .OrderByDescending(r => r.CombinedScore)
                .ThenByDescending(r => r.SemanticScore)
                .Take(5)
                .ToList();
        }

        // 4. Whole-word matching
        private static bool ContainsWholeTerm(
            string text, string term)
        {
            if (string.IsNullOrWhiteSpace(text) ||
                string.IsNullOrWhiteSpace(term))
                return false;

            string pattern =
                $@"\b{Regex.Escape(term)}\b";

            return Regex.IsMatch(
                text,
                pattern,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant
            );
        }

        // 5. Cosine Similarity Calculation
        private static double CalculateCosineSimilarity(
            List<float> vectorA,
            List<float> vectorB)
        {
            if (vectorA.Count != vectorB.Count ||
                vectorA.Count == 0)
                return 0;

            double dotProduct = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;

            for (int i = 0; i < vectorA.Count; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];

                magnitudeA += vectorA[i] * vectorA[i];

                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct /
                (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }
    }
}
