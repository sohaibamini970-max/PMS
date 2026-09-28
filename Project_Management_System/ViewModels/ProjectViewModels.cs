using System.ComponentModel.DataAnnotations;
using Project_Management_System.Models;

namespace Project_Management_System.ViewModels
{
    // ============================================
    // CREATE PROJECT (form data)
    // ============================================
    public class CreateProjectViewModel
    {
        [Required(ErrorMessage = "Project name is required")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Client { get; set; }

        [MaxLength(100)]
        public string? Domain { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }

        [Range(0, 999999999)]
        public decimal? Budget { get; set; }

        public Guid? ProjectManagerId { get; set; }
    }

    // ============================================
    // UPDATE PROJECT
    // ============================================
    public class UpdateProjectViewModel
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Client { get; set; }

        [MaxLength(100)]
        public string? Domain { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public ProjectPriority Priority { get; set; }
        public ProjectStatus Status { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public decimal? Budget { get; set; }
        public int Progress { get; set; }
        public Guid? ProjectManagerId { get; set; }
    }

    // ============================================
    // PROJECT LIST ITEM (for grid rendering)
    // ============================================
    public class ProjectListItemViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Client { get; set; }
        public string? Domain { get; set; }
        public string? Description { get; set; }
        public ProjectPriority Priority { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public decimal? Budget { get; set; }
        public int Progress { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public bool AllTasksCompleted => TotalTasks > 0 && TotalTasks == CompletedTasks;

        public string? AssignedTeamName { get; set; }
        public Guid? AssignedTeamId { get; set; }
        public string? AssignedMemberName { get; set; }
        public Guid? AssignedMemberId { get; set; }

        // Manager info
        public Guid? ProjectManagerId { get; set; }
        public string? ProjectManagerName { get; set; }
        public string? ProjectManagerPicture { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    // ============================================
    // PM DROPDOWN OPTION
    // ============================================
    public class ProjectManagerOptionViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
    }

    // ============================================
    // STATS (KPI cards)
    // ============================================
    public class ProjectStatsViewModel
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int AtRisk { get; set; }
        public int Completed { get; set; }
        public int OnHold { get; set; }
        public int Overdue { get; set; }
    }
}