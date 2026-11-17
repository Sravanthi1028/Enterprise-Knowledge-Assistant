
using System.ComponentModel.DataAnnotations;

namespace EnterpriseKnowledge.API.Models
{
    public class ChatMessage
    {
        [Key]
        public int MessageId { get; set; }

        public int ConversationId { get; set; }

        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Conversation? Conversation { get; set; }
    }
}
