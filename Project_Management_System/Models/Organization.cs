using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Management_System.Models
{
    public class Organization
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(200)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Slug { get; set; }

        public string? LogoUrl { get; set; }

        [MaxLength(20)]
        public string PrimaryColor { get; set; } = "#1D4ED8";

        [MaxLength(200)]
        public string? ContactEmail { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        public string? Address { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active";  // Active, Trial, Suspended, Expired

        public DateTime? ExpiresAt { get; set; }

        public Guid? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}