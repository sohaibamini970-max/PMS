using System.ComponentModel.DataAnnotations;

namespace Project_Management_System.Models
{
    public class Team
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Organization? Organization { get; set; }


        public Guid? LeaderId { get; set; }
        public User? Leader { get; set; }

        [MaxLength(150)] public string Name { get; set; } = "";
        [MaxLength(500)] public string? Description { get; set; }
        [MaxLength(30)] public string Color { get; set; } = "emerald";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }

    public class TeamMember
    {
        public Guid Id { get; set; }
        public Guid TeamId { get; set; }
        public Team Team { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}