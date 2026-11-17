
using System;
using System.ComponentModel.DataAnnotations;

namespace EnterpriseKnowledge.API.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = "Employee";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
