using System.ComponentModel.DataAnnotations;

namespace Project_Management_System.ViewModels
{
    public class CreateOrganizationViewModel
    {
        // Organization
        [Required(ErrorMessage = "Organization name is required")]
        [MaxLength(200)]
        public string OrganizationName { get; set; }

        [Required(ErrorMessage = "Subdomain is required")]
        [MaxLength(100)]
        [RegularExpression(@"^[a-z0-9\-]+$", ErrorMessage = "Only lowercase letters, numbers and hyphens")]
        public string Slug { get; set; }

        public string? ContactEmail { get; set; }
        public string? Phone { get; set; }
        public string Plan { get; set; } = "Pro";

        // First System Admin
        [Required(ErrorMessage = "Admin name is required")]
        public string AdminFullName { get; set; }

        [Required(ErrorMessage = "Admin email is required")]
        [EmailAddress]
        public string AdminEmail { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Minimum 8 characters")]
        public string AdminPassword { get; set; }

        [Compare("AdminPassword", ErrorMessage = "Passwords do not match")]
        public string AdminPasswordConfirm { get; set; }
    }

    // For the list view
    public class OrganizationListItemViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Status { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // System Admin info
        public string? AdminName { get; set; }
        public string? AdminEmail { get; set; }
        public Guid? AdminId { get; set; }

        // Stats
        public int TotalUsers { get; set; }
        public int TotalProjects { get; set; }
        public int TotalTasks { get; set; }
    }
}