
namespace EnterpriseKnowledge.API.Models
{
    public class HybridSearchResult
    {
        public int DocumentId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public int ChunkId { get; set; }

        public int ChunkIndex { get; set; }

        public string Content { get; set; } = string.Empty;

        public double SemanticScore { get; set; }

        public int KeywordScore { get; set; }

        public double CombinedScore { get; set; }
    }
}
