using System.ComponentModel.DataAnnotations;

namespace Project_Management_System.Models
{
    public class Document
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Organization? Organization { get; set; }
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }
        public Guid UploadedByUserId { get; set; }
        public User? UploadedBy { get; set; }

        [MaxLength(255)] public string Title { get; set; } = "";
        [MaxLength(255)] public string OriginalFileName { get; set; } = "";
        [MaxLength(255)] public string StoredFileName { get; set; } = "";
        [MaxLength(500)] public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        [MaxLength(150)] public string? ContentType { get; set; }
        [MaxLength(50)] public string DocumentType { get; set; } = "Other";
        [MaxLength(1000)] public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}