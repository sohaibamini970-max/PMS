using System.ComponentModel.DataAnnotations;

namespace Project_Management_System.Models
{
    public enum UserRole
    {
        SuperAdmin,
        SystemAdmin,
        ExecutiveManager,
        ProjectManager,
        Member
    }

    public enum UserStatus
    {
        Active,
        Inactive,
        Pending,
        Suspended
    }

    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Null ONLY for SuperAdmin
        public Guid? OrganizationId { get; set; }

        [Required, MaxLength(200)]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required, MaxLength(200)]
        public string FullName { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        public string? ProfilePictureUrl { get; set; }

        [Required]
        public UserRole Role { get; set; }

        [Required]
        public UserStatus Status { get; set; } = UserStatus.Active;

        public Guid? CreatedByUserId { get; set; }

        public bool EmailConfirmed { get; set; } = false;

        public DateTime? LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Organization? Organization { get; set; }
        public User? CreatedBy { get; set; }
    }
}