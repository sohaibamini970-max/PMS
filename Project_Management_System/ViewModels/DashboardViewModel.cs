namespace Project_Management_System.ViewModels
{
    public class DashboardViewModel
    {
        public string UserFullName { get; set; } = "";
        public string Role { get; set; } = "";

        // Top KPIs — projects
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int OverdueProjects { get; set; }

        // Task breakdown for the "Project Health" box
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int OverdueTasks { get; set; }

        // Computed
        public int OverallCompletion { get; set; }

        // Chart data
        public List<DashboardProjectItem> ChartProjects { get; set; } = new();

        // Recent projects table
        public List<RecentProjectItem> RecentProjects { get; set; } = new();

        // Upcoming deadlines
        public List<UpcomingDeadlineItem> UpcomingDeadlines { get; set; } = new();
    }

    public class DashboardProjectItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Percent { get; set; }
        public string Status { get; set; } = "";
        public string Manager { get; set; } = "";
        public string Budget { get; set; } = "—";
        public string Desc { get; set; } = "";
    }

    public class RecentProjectItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Initials { get; set; } = "";
        public string Client { get; set; } = "";
        public DateTime? Deadline { get; set; }
        public int Progress { get; set; }
        public string Status { get; set; } = "";
    }

    public class UpcomingDeadlineItem
    {
        public string Title { get; set; } = "";
        public string When { get; set; } = "";
        public string Color { get; set; } = "blue"; // blue | purple | amber | red
    }
}