
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EnterpriseKnowledge.API.Models
{
    public class DocumentChunk
    {
        [Key]
        public int ChunkId { get; set; }

        [Required]
        public int DocumentId { get; set; }

        [Required]
        public int ChunkIndex { get; set; }

        [Required]
        [Column(TypeName = "longtext")]
        public string Content { get; set; } = string.Empty;

        // Stores the 768-dimensional embedding as JSON
        [Column(TypeName = "longtext")]
        public string? Embedding { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Document? Document { get; set; }
    }
}
