namespace Project_Management_System.ViewModels
{
    public class TeamsIndexViewModel
    {
        public List<ProjectWithTasksViewModel> Projects { get; set; } = new();
        public List<TeamViewModel> Teams { get; set; } = new();
        public List<MemberViewModel> Members { get; set; } = new();
        public bool CanManage { get; set; }
        public Guid CurrentUserId { get; set; }

        public bool IsMemberRole { get; set; }
        public bool IsTeamLeader { get; set; }
        public List<Guid> LeadingTeamIds { get; set; } = new();
    }

    public class TeamViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Color { get; set; } = "emerald";
        public List<TeamMemberViewModel> Members { get; set; } = new();
        public int ProjectCount { get; set; }
        public int TaskCount { get; set; }

        public Guid? LeaderId { get; set; }
        public string? LeaderName { get; set; }
        public string? LeaderPicture { get; set; }
    }

    public class TeamMemberViewModel
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = "";
        public string? ProfilePictureUrl { get; set; }
        public string Role { get; set; } = "";
        public bool IsLeader { get; set; }
    }

    public class MemberViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? ProfilePictureUrl { get; set; }
        public string Role { get; set; } = "";
        public int ProjectCount { get; set; }
        public int TaskCount { get; set; }
    }
}