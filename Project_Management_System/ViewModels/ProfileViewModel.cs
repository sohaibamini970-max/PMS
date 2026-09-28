namespace Project_Management_System.ViewModels
{
    public class ProfileViewModel
    {
        // ── User info ──
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string RoleDisplay { get; set; } = "";
        public string? ProfilePictureUrl { get; set; }
        public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; }
        public string MemberSince => CreatedAt.ToString("MMM yyyy");
        public string AvatarUrl =>
            !string.IsNullOrWhiteSpace(ProfilePictureUrl)
                ? ProfilePictureUrl!
                : $"https://i.pravatar.cc/200?u={Id}";

        // ── Quick stats ──
        public int TotalProjects { get; set; }
        public int TotalTasks { get; set; }
        public int TotalTeams { get; set; }
        public int SuccessRate { get; set; }

        // ── Task breakdown ──
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }

        // ── Section titles (role-aware) ──
        public string SidePanelTitle { get; set; } = "";
        public string ProjectsSectionLabel { get; set; } = "";
        public string RecentTasksTitle { get; set; } = "";
        public string ProfileRoleBadge { get; set; } = "";

        // ── Side stat cards ──
        public List<ProfileStatCard> SideStats { get; set; } = new();

        // ── Monthly chart ──
        public List<string> MonthlyLabels { get; set; } = new();
        public List<int> MonthlyCompleted { get; set; } = new();
        public List<int> MonthlyPending { get; set; } = new();

        // ── Projects & tasks lists ──
        public List<ProfileProjectItem> Projects { get; set; } = new();
        public List<ProfileTaskItem> RecentTasks { get; set; } = new();
    }

    public class ProfileStatCard
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string Subtitle { get; set; } = "";
        // "blue" | "emerald" | "purple" | "amber"
        public string Color { get; set; } = "blue";
    }

    public class ProfileProjectItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Client { get; set; } = "";
        public string Status { get; set; } = "";
        public int Progress { get; set; }
        public int TaskCount { get; set; }
        public DateTime? Deadline { get; set; }
    }

    public class ProfileTaskItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Priority { get; set; } = "";
        public DateTime? Deadline { get; set; }
        public string Status { get; set; } = "";
    }
}