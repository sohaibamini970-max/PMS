using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Management_System.Data;
using Project_Management_System.Models;
using Project_Management_System.ViewModels;
using System.Security.Claims;

namespace Project_Management_System.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        // Only these extensions are allowed
        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx" };

        private static readonly string[] AllowedContentTypes =
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

        private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

        public DocumentsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private Guid CurrentOrgId => Guid.Parse(User.FindFirst("OrganizationId")!.Value);
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        private bool IsExec => CurrentRole == "ExecutiveManager";
        private bool IsAdmin => CurrentRole == "SystemAdmin";
        private bool IsPM => CurrentRole == "ProjectManager";
        private bool IsMember => CurrentRole == "Member";

        // ============================================
        // GET: /Documents
        // ============================================
        public async Task<IActionResult> Index()
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            // Teams this user leads (if Member)
            var leadingTeamIds = IsMember
                ? await _db.Teams
                    .Where(t => t.OrganizationId == orgId && t.LeaderId == userId)
                    .Select(t => t.Id)
                    .ToListAsync()
                : new List<Guid>();

            var leaderTeamMemberIds = leadingTeamIds.Any()
                ? await _db.TeamMembers
                    .Where(tm => leadingTeamIds.Contains(tm.TeamId))
                    .Select(tm => tm.UserId)
                    .Distinct()
                    .ToListAsync()
                : new List<Guid>();

            // ── Projects in scope ──
            IQueryable<Project> projectQuery = _db.Projects.Where(p => p.OrganizationId == orgId);

            if (IsPM)
            {
                projectQuery = projectQuery.Where(p => p.ProjectManagerId == userId);
            }
            else if (IsMember)
            {
                projectQuery = projectQuery.Where(p =>
                    (p.AssignedMemberId == userId) ||
                    (p.AssignedTeamId != null && leadingTeamIds.Contains(p.AssignedTeamId.Value)) ||
                    _db.Tasks.Any(t => t.ProjectId == p.Id &&
                        (t.AssigneeId == userId ||
                         (t.AssigneeTeamId != null && leadingTeamIds.Contains(t.AssigneeTeamId.Value)) ||
                         (t.AssigneeId != null && leaderTeamMemberIds.Contains(t.AssigneeId.Value)))));
            }

            var projectRows = await projectQuery
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => new { p.Id, p.Name, p.Client })
                .ToListAsync();

            var projectIds = projectRows.Select(p => p.Id).ToList();

            // ── Documents in those projects ──
            var documentRows = await _db.Documents
                .Where(d => d.OrganizationId == orgId && projectIds.Contains(d.ProjectId))
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new
                {
                    d.Id,
                    d.ProjectId,
                    d.Title,
                    d.OriginalFileName,
                    d.FileSize,
                    d.DocumentType,
                    d.Notes,
                    d.CreatedAt,
                    UploadedByName = d.UploadedBy != null ? d.UploadedBy.FullName : "Unknown"
                })
                .ToListAsync();

            var docsByProject = documentRows
                .GroupBy(d => d.ProjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ── Colour palette for projects ──
            string[] palette = { "emerald", "amber", "blue", "purple", "red" };

            var projectGroups = new List<ProjectDocumentsGroup>();
            for (int i = 0; i < projectRows.Count; i++)
            {
                var p = projectRows[i];
                var docs = docsByProject.TryGetValue(p.Id, out var d) ? d : new();

                projectGroups.Add(new ProjectDocumentsGroup
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    Client = p.Client,
                    Initials = p.Name.Length >= 2
                        ? p.Name.Substring(0, 2).ToUpper()
                        : p.Name.Substring(0, 1).ToUpper(),
                    Color = palette[i % palette.Length],
                    Documents = docs.Select(x => new DocumentItem
                    {
                        Id = x.Id,
                        Title = x.Title,
                        OriginalFileName = x.OriginalFileName,
                        Extension = Path.GetExtension(x.OriginalFileName).TrimStart('.').ToUpper(),
                        FileSize = x.FileSize,
                        FormattedSize = FormatBytes(x.FileSize),
                        DocumentType = x.DocumentType,
                        Notes = x.Notes,
                        UploadedAt = x.CreatedAt,
                        UploadedByName = x.UploadedByName,
                        DownloadUrl = Url.Action("Download", "Documents", new { id = x.Id }) ?? "#"
                    }).ToList()
                });
            }

            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);

            var vm = new DocumentsIndexViewModel
            {
                TotalDocuments = documentRows.Count,
                TotalStorageBytes = documentRows.Sum(d => d.FileSize),
                UploadsThisWeek = documentRows.Count(d => d.CreatedAt >= weekAgo),
                ProjectCount = projectRows.Count,

                // Upload allowed for PM / Exec / Admin (not Members)
                CanUpload = IsPM || IsExec || IsAdmin,
                CurrentRole = CurrentRole,

                Projects = projectGroups,
                AvailableProjects = projectRows.Select((p, i) => new ProjectOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Initials = p.Name.Length >= 2 ? p.Name.Substring(0, 2).ToUpper() : p.Name.Substring(0, 1).ToUpper(),
                    Color = palette[i % palette.Length]
                }).ToList()
            };

            return View(vm);
        }

        // ============================================
        // POST: /Documents/Upload
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileSizeBytes)]
        public async Task<IActionResult> Upload(
            Guid projectId,
            string title,
            string documentType,
            string? notes,
            IFormFile file)
        {
            if (!IsPM && !IsExec && !IsAdmin)
                return Json(new { success = false, message = "Only Project Managers, Executive Managers and System Admins can upload documents." });

            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select a file." });

            if (file.Length > MaxFileSizeBytes)
                return Json(new { success = false, message = "File exceeds the 50 MB limit." });

            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileNameWithoutExtension(file.FileName);

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == CurrentOrgId);
            if (project == null)
                return Json(new { success = false, message = "Project not found." });

            if (IsPM && project.ProjectManagerId != CurrentUserId)
                return Json(new { success = false, message = "You can only upload to your own projects." });

            // ── Validate extension / content type ──
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return Json(new { success = false, message = "Only PDF and Word documents (.pdf, .doc, .docx) are allowed." });

            if (!string.IsNullOrEmpty(file.ContentType) &&
                !AllowedContentTypes.Any(ct => file.ContentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase)))
                return Json(new { success = false, message = "Invalid file type." });

            // ── Save file under wwwroot/uploads/documents/{orgId}/{projectId}/ ──
            var orgIdStr = CurrentOrgId.ToString();
            var projectIdStr = projectId.ToString();
            var storedName = $"{Guid.NewGuid():N}{ext}";
            var relativeDir = Path.Combine("uploads", "documents", orgIdStr, projectIdStr);
            var absoluteDir = Path.Combine(_env.WebRootPath, relativeDir);
            Directory.CreateDirectory(absoluteDir);

            var absolutePath = Path.Combine(absoluteDir, storedName);
            var relativePath = Path.Combine(relativeDir, storedName).Replace("\\", "/");

            using (var stream = System.IO.File.Create(absolutePath))
            {
                await file.CopyToAsync(stream);
            }

            var doc = new Document
            {
                Id = Guid.NewGuid(),
                OrganizationId = CurrentOrgId,
                ProjectId = projectId,
                UploadedByUserId = CurrentUserId,
                Title = title.Trim(),
                OriginalFileName = file.FileName,
                StoredFileName = storedName,
                FilePath = relativePath,
                FileSize = file.Length,
                ContentType = file.ContentType,
                DocumentType = string.IsNullOrWhiteSpace(documentType) ? "Other" : documentType,
                Notes = notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Documents.Add(doc);
            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"'{doc.Title}' uploaded successfully.",
                documentId = doc.Id
            });
        }

        // ============================================
        // GET: /Documents/Download/{id}
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Download(Guid id)
        {
            var orgId = CurrentOrgId;

            var doc = await _db.Documents
                .Include(d => d.Project)
                .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == orgId);
            if (doc == null) return NotFound();

            // Access check — same as the index filter
            var allowed = await CanAccessProjectAsync(doc.ProjectId);
            if (!allowed) return Forbid();

            var absolutePath = Path.Combine(_env.WebRootPath, doc.FilePath);
            if (!System.IO.File.Exists(absolutePath))
                return NotFound("File missing on server.");

            var contentType = doc.ContentType ?? "application/octet-stream";
            return PhysicalFile(absolutePath, contentType, doc.OriginalFileName);
        }

        // ============================================
        // GET: /Documents/Preview/{id}
        // ============================================
        [HttpGet]
 
        public async Task<IActionResult> Preview(Guid id)
        {
            var orgId = CurrentOrgId;

            var doc = await _db.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == orgId);
            if (doc == null) return NotFound();

            var allowed = await CanAccessProjectAsync(doc.ProjectId);
            if (!allowed) return Forbid();

            var absolutePath = Path.Combine(_env.WebRootPath, doc.FilePath);
            if (!System.IO.File.Exists(absolutePath))
                return NotFound("File missing on server.");

            var contentType = doc.ContentType ?? "application/octet-stream";

            // Explicit inline header so the browser never downloads
            Response.Headers.Append("Content-Disposition",
                $"inline; filename=\"{Uri.EscapeDataString(doc.OriginalFileName)}\"");

            return PhysicalFile(absolutePath, contentType, enableRangeProcessing: true);
        }

        // ============================================
        // POST: /Documents/Delete/{id}
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var doc = await _db.Documents
                .Include(d => d.Project)
                .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == CurrentOrgId);
            if (doc == null) return Json(new { success = false, message = "Document not found." });

            // Exec / Admin → any document. PM → own project. Member → deny.
            bool canDelete =
                IsExec || IsAdmin ||
                (IsPM && doc.Project?.ProjectManagerId == CurrentUserId);

            if (!canDelete)
                return Json(new { success = false, message = "You don't have permission to delete this document." });

            // Remove file from disk
            try
            {
                var absolutePath = Path.Combine(_env.WebRootPath, doc.FilePath);
                if (System.IO.File.Exists(absolutePath))
                    System.IO.File.Delete(absolutePath);
            }
            catch { /* best-effort */ }

            _db.Documents.Remove(doc);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Document deleted." });
        }

        // ============================================
        // Helpers
        // ============================================
        private async Task<bool> CanAccessProjectAsync(Guid projectId)
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            if (IsExec || IsAdmin) return true;

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == orgId);
            if (project == null) return false;

            if (IsPM) return project.ProjectManagerId == userId;

            if (IsMember)
            {
                var leadingTeamIds = await _db.Teams
                    .Where(t => t.OrganizationId == orgId && t.LeaderId == userId)
                    .Select(t => t.Id)
                    .ToListAsync();

                var leaderTeamMemberIds = leadingTeamIds.Any()
                    ? await _db.TeamMembers
                        .Where(tm => leadingTeamIds.Contains(tm.TeamId))
                        .Select(tm => tm.UserId).Distinct().ToListAsync()
                    : new List<Guid>();

                if (project.AssignedMemberId == userId) return true;
                if (project.AssignedTeamId != null && leadingTeamIds.Contains(project.AssignedTeamId.Value)) return true;

                return await _db.Tasks.AnyAsync(t => t.ProjectId == projectId &&
                    (t.AssigneeId == userId ||
                     (t.AssigneeTeamId != null && leadingTeamIds.Contains(t.AssigneeTeamId.Value)) ||
                     (t.AssigneeId != null && leaderTeamMemberIds.Contains(t.AssigneeId.Value))));
            }

            return false;
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int i = 0;
            while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
            return $"{size:0.#} {units[i]}";
        }
    }
}