
using System.ComponentModel.DataAnnotations;

namespace EnterpriseKnowledge.API.DTOs
{
    public class DocumentSearchRequestDto
    {
        [Required(ErrorMessage = "Search query is required")]
        [StringLength(200, MinimumLength = 2)]
        public string Query { get; set; } = string.Empty;

        public int? DocumentId { get; set; }
    }
}
