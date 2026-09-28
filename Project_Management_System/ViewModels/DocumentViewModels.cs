namespace Project_Management_System.ViewModels
{
    public class DocumentsIndexViewModel
    {
        public int TotalDocuments { get; set; }
        public long TotalStorageBytes { get; set; }
        public int UploadsThisWeek { get; set; }
        public int ProjectCount { get; set; }

        public List<ProjectDocumentsGroup> Projects { get; set; } = new();
        public List<ProjectOptionViewModel> AvailableProjects { get; set; } = new();

        public bool CanUpload { get; set; }
        public string CurrentRole { get; set; } = "";

        public string FormattedStorage
        {
            get
            {
                var b = TotalStorageBytes;
                string[] units = { "B", "KB", "MB", "GB" };
                double size = b;
                int i = 0;
                while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
                return $"{size:0.#} {units[i]}";
            }
        }
    }

    public class ProjectDocumentsGroup
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string? Client { get; set; }
        public string Initials { get; set; } = "";
        public string Color { get; set; } = "emerald";
        public List<DocumentItem> Documents { get; set; } = new();
    }

    public class DocumentItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string OriginalFileName { get; set; } = "";
        public string Extension { get; set; } = "";
        public long FileSize { get; set; }
        public string FormattedSize { get; set; } = "";
        public string DocumentType { get; set; } = "";
        public string? Notes { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedByName { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }

    public class ProjectOptionViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Initials { get; set; } = "";
        public string Color { get; set; } = "emerald";
    }
}