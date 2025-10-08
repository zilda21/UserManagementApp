using System.ComponentModel.DataAnnotations;

namespace UserManagementApp.Models
{
    public class User
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Status { get; set; } = "unverified";

        public DateTime? LastLogin { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(510)]
        public string? VerificationToken { get; set; }
    }
}
