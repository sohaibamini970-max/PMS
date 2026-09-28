using System.ComponentModel.DataAnnotations;
using Project_Management_System.Models;

namespace Project_Management_System.ViewModels
{
    // ============================================
    // CREATE USER (from System Admin portal)
    // ============================================
    public class CreateOrgUserViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [MaxLength(200)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Role is required")]
        public UserRole Role { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string PasswordConfirm { get; set; }
    }

    // ============================================
    // UPDATE USER
    // ============================================
    public class UpdateOrgUserViewModel
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [Required]
        public UserRole Role { get; set; }

        public UserStatus Status { get; set; }
    }

    // ============================================
    // USER LIST ITEM (for the table)
    // ============================================
    public class OrgUserListItemViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }

    // ============================================
    // ADMIN DASHBOARD STATS
    // ============================================
    public class AdminStatsViewModel
    {
        public int TotalUsers { get; set; }
        public int ExecutiveManagers { get; set; }
        public int ProjectManagers { get; set; }
        public int Members { get; set; }
        public int SystemAdmins { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
    }

    // 5. ✅ Role info (NEW — this is the one you're missing)
    public class RoleInfoViewModel
    {
        public UserRole Role { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public string Color { get; set; } = "blue";
        public string Icon { get; set; } = "user";
        public string[] Permissions { get; set; } = Array.Empty<string>();
    }
}