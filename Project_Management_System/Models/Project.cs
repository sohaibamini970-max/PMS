using System.ComponentModel.DataAnnotations;

namespace Project_Management_System.Models
{
    public enum ProjectPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ProjectStatus
    {
        Planning,
        Active,
        AtRisk,
        OnHold,
        Completed,
        Done,
        Archived
    }

    public class Project
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrganizationId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Client { get; set; }
        public string? Description { get; set; }
        public string? Domain { get; set; }

        public Guid? AssignedTeamId { get; set; }
        public Team? AssignedTeam { get; set; }

        public Guid? AssignedMemberId { get; set; }
        public User? AssignedMember { get; set; }

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public decimal? Budget { get; set; }
        public int Progress { get; set; } = 0;

        public Guid? ProjectManagerId { get; set; }

        public Guid CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Organization? Organization { get; set; }
        public User? ProjectManager { get; set; }
        public User? CreatedBy { get; set; }
    }
}