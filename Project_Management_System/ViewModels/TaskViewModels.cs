using System.ComponentModel.DataAnnotations;
using Project_Management_System.Models;
using TaskStatus = Project_Management_System.Models.TaskStatus;

namespace Project_Management_System.ViewModels
{
    public class CreateTaskViewModel
    {
        [Required] public Guid ProjectId { get; set; }
        [Required, MaxLength(200)] public string Title { get; set; } = "";
        [MaxLength(2000)] public string? Description { get; set; }
        [MaxLength(2000)] public string? Objectives { get; set; }
        [MaxLength(5000)] public string? InstructionsText { get; set; }
        public string? InstructionsFileUrl { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public Guid? AssigneeId { get; set; }
        public Guid? AssigneeTeamId { get; set; }   // ⬅️ NEW
    }

    public class TaskListItemViewModel
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? Objectives { get; set; }
        public string? InstructionsText { get; set; }
        public string? InstructionsFileUrl { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }

        public Guid? AssigneeTeamId { get; set; }
        public string? AssigneeTeamName { get; set; }

        public TaskPriority Priority { get; set; }
        public TaskStatus Status { get; set; }

        public Guid? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
        public string? AssigneePicture { get; set; }

        public DateTime? SubmittedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public int SubtaskCount { get; set; }
        public int SubtaskDone { get; set; }
        public int ChallengeCount { get; set; }

        public bool IsOverdue => Deadline.HasValue
            && Deadline.Value.Date < DateTime.UtcNow.Date
            && Status != TaskStatus.Completed;
    }

    public class ProjectWithTasksViewModel
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string? ProjectClient { get; set; }
        public ProjectStatus ProjectStatus { get; set; }
        public string ProjectInitials { get; set; } = "";
        public int ProjectProgress { get; set; }

        // ⬇️ NEW — project-level team/member assignment
        public Guid? AssignedTeamId { get; set; }
        public string? AssignedTeamName { get; set; }
        public Guid? AssignedMemberId { get; set; }
        public string? AssignedMemberName { get; set; }

        public List<TaskListItemViewModel> Tasks { get; set; } = new();
    }

    public class SubtaskViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public SubtaskStatus Status { get; set; }
        public int OrderIndex { get; set; }
    }

    public class ChallengeViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public ChallengeStatus Status { get; set; }
        public string RaisedByName { get; set; } = "";
        public string? RaisedByPicture { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TasksIndexViewModel
    {
        public List<ProjectWithTasksViewModel> Projects { get; set; } = new();
        public List<MemberOptionViewModel> Members { get; set; } = new();
        public bool CanCreate { get; set; }
        public bool IsMember { get; set; }
        public bool IsPM { get; set; }
        public string CurrentRole { get; set; } = "";
    }

    public class MemberOptionViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
    }

    // ═════════════════════════════════════════════
    // TEAMS VIEW MODELS
    // ═════════════════════════════════════════════
    //public class TeamsIndexViewModel
    //{
    //    public List<ProjectWithTasksViewModel> Projects { get; set; } = new();
    //    public List<TeamViewModel> Teams { get; set; } = new();
    //    public List<MemberViewModel> Members { get; set; } = new();
    //    public bool CanManage { get; set; }
    //    public Guid CurrentUserId { get; set; }
    //}

    //public class TeamViewModel
    //{
    //    public Guid Id { get; set; }
    //    public string Name { get; set; } = "";
    //    public string? Description { get; set; }
    //    public string Color { get; set; } = "emerald";
    //    public List<TeamMemberViewModel> Members { get; set; } = new();
    //    public int ProjectCount { get; set; }
    //    public int TaskCount { get; set; }
    //}

    //public class TeamMemberViewModel
    //{
    //    public Guid UserId { get; set; }
    //    public string FullName { get; set; } = "";
    //    public string? ProfilePictureUrl { get; set; }
    //    public string Role { get; set; } = "";
    //}

    //public class MemberViewModel
    //{
    //    public Guid Id { get; set; }
    //    public string FullName { get; set; } = "";
    //    public string Email { get; set; } = "";
    //    public string? ProfilePictureUrl { get; set; }
    //    public string Role { get; set; } = "";
    //    public int ProjectCount { get; set; }
    //    public int TaskCount { get; set; }
    //}
}