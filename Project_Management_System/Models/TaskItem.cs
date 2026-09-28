namespace Project_Management_System.Models
{
    public enum TaskStatus { Planning, ToDo, InProgress, Done, Completed }
    public enum TaskPriority { Low, Medium, High, Critical }

    public class TaskItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrganizationId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? AssigneeTeamId { get; set; }
        public Team? AssigneeTeam { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Objectives { get; set; }
        public string? InstructionsText { get; set; }
        public string? InstructionsFileUrl { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public TaskStatus Status { get; set; } = TaskStatus.ToDo;

        public Guid? AssigneeId { get; set; }
        public Guid CreatedByUserId { get; set; }

        public DateTime? SubmittedAt { get; set; }
        public Guid? SubmittedById { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid? CompletedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Nav
        public Organization? Organization { get; set; }
        public Project? Project { get; set; }
        public User? Assignee { get; set; }
        public User? CreatedBy { get; set; }
        public List<TaskSubtask> Subtasks { get; set; } = new();
        public List<TaskChallenge> Challenges { get; set; } = new();
    }
}