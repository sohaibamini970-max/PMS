namespace Project_Management_System.ViewModels
{
    public class TrackingViewModel
    {
        public int TotalProjects { get; set; }
        public int TotalCompletedTasks { get; set; }
        public int TotalPendingTasks { get; set; }
        public int TotalOverdueTasks { get; set; }

        public List<TeamTrackingItem> Teams { get; set; } = new();
        public List<MemberTrackingItem> Members { get; set; } = new();
    }

    public class TeamTrackingItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "emerald";
        public int MemberCount { get; set; }
        public int ProjectCount { get; set; }

        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int TotalTasks => CompletedTasks + PendingTasks + OverdueTasks;
        public int CompletionRate { get; set; }

        public string RankMedal { get; set; } = "";
        public List<ProjectBreakdownItem> Projects { get; set; } = new();
    }

    public class MemberTrackingItem
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? ProfilePictureUrl { get; set; }
        public string Role { get; set; } = "";
        public int ProjectCount { get; set; }

        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int TotalTasks => CompletedTasks + PendingTasks + OverdueTasks;
        public int CompletionRate { get; set; }

        public string RankMedal { get; set; } = "";
        public List<ProjectBreakdownItem> Projects { get; set; } = new();
    }

    public class ProjectBreakdownItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Completed { get; set; }
        public int Pending { get; set; }
        public int Overdue { get; set; }
        public int CompletionRate { get; set; }
    }
}