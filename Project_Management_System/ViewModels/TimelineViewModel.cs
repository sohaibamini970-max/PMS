namespace Project_Management_System.ViewModels
{
    public class TimelineViewModel
    {
        public int TotalProjects { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public List<TimelineProjectItem> Projects { get; set; } = new();
    }

    public class TimelineProjectItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Client { get; set; } = "";
        public string Initials { get; set; } = "";
        public string Status { get; set; } = "";

        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public int ProjectSpanDays { get; set; }
        public string ProjectSpanLabel { get; set; } = "";

        public int CompletedCount { get; set; }
        public int PendingCount { get; set; }
        public int OverdueCount { get; set; }

        public List<TimelineTaskItem> Tasks { get; set; } = new();
    }

    public class TimelineTaskItem
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";

        // "Completed" | "Pending" | "Overdue"
        public string Status { get; set; } = "";

        public DateTime? StartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public string DateRangeLabel { get; set; } = "";
        public string StatusNote { get; set; } = "";

        // Bar position within project span (0–100)
        public double LeftPercent { get; set; }
        public double WidthPercent { get; set; }
        public string DurationLabel { get; set; } = "";
    }
}