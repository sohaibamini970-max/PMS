namespace Project_Management_System.Models
{
    public enum SubtaskStatus { Planning, ToDo, InProgress, Done }

    public class TaskSubtask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public SubtaskStatus Status { get; set; } = SubtaskStatus.Planning;
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public TaskItem? Task { get; set; }
    }
}