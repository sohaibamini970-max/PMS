namespace Project_Management_System.Models
{
    public enum ChallengeStatus { ToDo, InProgress, Done }

    public class TaskChallenge
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TaskId { get; set; }
        public Guid RaisedByUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ChallengeStatus Status { get; set; } = ChallengeStatus.ToDo;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public TaskItem? Task { get; set; }
        public User? RaisedBy { get; set; }
    }
}