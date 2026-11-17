
namespace EnterpriseKnowledge.API.Models
{
    public class RAGSource
    {
        public int DocumentId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public int ChunkId { get; set; }

        public int ChunkIndex { get; set; }

        public string Content { get; set; } = string.Empty;
    }
}
