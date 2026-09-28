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
    public class ProjectsController : Controller
    {
        private readonly AppDbContext _db;

        public ProjectsController(AppDbContext db)
        {
            _db = db;
        }

        // ============================================
        // HELPERS
        // ============================================
        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private Guid CurrentOrgId =>
            Guid.Parse(User.FindFirst("OrganizationId")!.Value);

        private string CurrentRole =>
            User.FindFirst(ClaimTypes.Role)?.Value ?? "";

        private bool IsExecutive => CurrentRole == "ExecutiveManager";
        private bool IsPM => CurrentRole == "ProjectManager";
        private bool IsAdmin => CurrentRole == "SystemAdmin";

        // ============================================
        // GET: /Projects
        // ============================================
        public async Task<IActionResult> Index()
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            // Base query — always scoped to the org
            var query = _db.Projects
                .Where(p => p.OrganizationId == orgId);

            // ─── ROLE-BASED FILTERING ───
            // PMs see only their assigned projects
            if (IsPM)
            {
                query = query.Where(p => p.ProjectManagerId == userId);
            }
            // Members see only projects they're assigned to (once we build task assignments)
            // else if (CurrentRole == "Member") { ... }
            // Executive Managers and System Admins see ALL org projects

            // Load with manager info
            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectListItemViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Client = p.Client,
                    Domain = p.Domain,
                    Description = p.Description,
                    Priority = p.Priority,
                    Status = p.Status,
                    StartDate = p.StartDate,
                    Deadline = p.Deadline,
                    Budget = p.Budget,
                    Progress = p.Progress,
                    ProjectManagerId = p.ProjectManagerId,
                    ProjectManagerName = p.ProjectManager != null ? p.ProjectManager.FullName : null,
                    ProjectManagerPicture = p.ProjectManager != null ? p.ProjectManager.ProfilePictureUrl : null,
                    CreatedAt = p.CreatedAt,
                    TotalTasks = _db.Tasks.Count(t => t.ProjectId == p.Id),
                    CompletedTasks = _db.Tasks.Count(t => t.ProjectId == p.Id
                                                        && t.Status == Project_Management_System.Models.TaskStatus.Completed),
                })
                .ToListAsync();

            // ─── STATS ───
            var today = DateTime.UtcNow.Date;
            ViewBag.Stats = new ProjectStatsViewModel
            {
                Total = projects.Count,
                Active = projects.Count(p => p.Status == ProjectStatus.Active),
                AtRisk = projects.Count(p => p.Status == ProjectStatus.AtRisk),
                Completed = projects.Count(p => p.Status == ProjectStatus.Completed),
                OnHold = projects.Count(p => p.Status == ProjectStatus.OnHold),
                Overdue = projects.Count(p => p.Deadline.HasValue
                                            && p.Deadline.Value.Date < today
                                            && p.Status != ProjectStatus.Completed
                                            && p.Status != ProjectStatus.Archived)
            };

            // ─── PM DROPDOWN (only for those who can create) ───
            if (IsExecutive || IsAdmin)
            {
                ViewBag.ProjectManagers = await _db.Users
                    .Where(u => u.OrganizationId == orgId
                             && u.Role == UserRole.ProjectManager
                             && u.Status == UserStatus.Active)
                    .OrderBy(u => u.FullName)
                    .Select(u => new ProjectManagerOptionViewModel
                    {
                        Id = u.Id,
                        FullName = u.FullName,
                        Email = u.Email,
                        ProfilePictureUrl = u.ProfilePictureUrl
                    })
                    .ToListAsync();
            }
            else
            {
                ViewBag.ProjectManagers = new List<ProjectManagerOptionViewModel>();
            }

            ViewBag.CanCreate = IsExecutive || IsAdmin;
            ViewBag.CurrentRole = CurrentRole;
            ViewBag.CurrentUserId = userId;

            return View(projects);
        }

        // ============================================
        // POST: /Projects/CreateProject
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ExecutiveManager,SystemAdmin")]
        public async Task<IActionResult> CreateProject(CreateProjectViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            if (model.StartDate.HasValue && model.Deadline.HasValue
                && model.Deadline.Value.Date < model.StartDate.Value.Date)
            {
                return Json(new { success = false, message = "Deadline cannot be before start date." });
            }

            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            if (model.ProjectManagerId.HasValue)
            {
                var pmValid = await _db.Users.AnyAsync(u =>
                    u.Id == model.ProjectManagerId.Value &&
                    u.OrganizationId == orgId &&
                    u.Role == UserRole.ProjectManager &&
                    u.Status == UserStatus.Active);

                if (!pmValid)
                    return Json(new { success = false, message = "Invalid project manager selection." });
            }

            var project = new Project
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                Name = model.Name.Trim(),
                Client = model.Client?.Trim(),
                Domain = model.Domain?.Trim(),
                Description = model.Description?.Trim(),
                Priority = model.Priority,
                Status = model.Status,

                // ✅ FIX: Force UTC Kind so Npgsql accepts them
                StartDate = model.StartDate.HasValue
                    ? DateTime.SpecifyKind(model.StartDate.Value.Date, DateTimeKind.Utc)
                    : (DateTime?)null,
                Deadline = model.Deadline.HasValue
                    ? DateTime.SpecifyKind(model.Deadline.Value.Date, DateTimeKind.Utc)
                    : (DateTime?)null,

                Budget = model.Budget,
                Progress = 0,
                ProjectManagerId = model.ProjectManagerId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Projects.Add(project);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                var inner = dbEx.InnerException?.Message ?? "no inner";
                var deepest = dbEx.InnerException?.InnerException?.Message ?? "no deepest";
                return Json(new { success = false, message = $"DB: {inner} | DEEP: {deepest}" });
            }

            return Json(new
            {
                success = true,
                message = $"Project '{project.Name}' created successfully.",
                projectId = project.Id
            });
        }

        // ============================================
        // GET: /Projects/GetProject/{id}
        // ============================================
        [HttpGet]
        public async Task<IActionResult> GetProject(Guid id)
        {
            var orgId = CurrentOrgId;

            var project = await _db.Projects
                .Where(p => p.Id == id && p.OrganizationId == orgId)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    client = p.Client,
                    domain = p.Domain,
                    description = p.Description,
                    priority = p.Priority.ToString(),
                    status = p.Status.ToString(),
                    startDate = p.StartDate.HasValue ? p.StartDate.Value.ToString("yyyy-MM-dd") : null,
                    deadline = p.Deadline.HasValue ? p.Deadline.Value.ToString("yyyy-MM-dd") : null,
                    budget = p.Budget,
                    progress = p.Progress,
                    projectManagerId = p.ProjectManagerId,
                    projectManagerName = p.ProjectManager != null ? p.ProjectManager.FullName : null,
                    projectManagerPicture = p.ProjectManager != null ? p.ProjectManager.ProfilePictureUrl : null,
                    createdAt = p.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (project == null) return NotFound();
            return Json(project);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProject(UpdateProjectViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            // Only Exec / Admin can edit project details
            if (!IsExecutive && !IsAdmin)
                return Json(new { success = false, message = "Only Executive Managers and System Admins can edit projects." });

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == model.Id && p.OrganizationId == orgId);

            if (project == null)
                return Json(new { success = false, message = "Project not found." });

            if (model.StartDate.HasValue && model.Deadline.HasValue
                && model.Deadline.Value.Date < model.StartDate.Value.Date)
                return Json(new { success = false, message = "Deadline cannot be before start date." });

            project.Name = model.Name.Trim();
            project.Client = model.Client?.Trim();
            project.Domain = model.Domain?.Trim();
            project.Description = model.Description?.Trim();
            project.Priority = model.Priority;

            project.StartDate = model.StartDate.HasValue
                ? DateTime.SpecifyKind(model.StartDate.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;
            project.Deadline = model.Deadline.HasValue
                ? DateTime.SpecifyKind(model.Deadline.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;

            project.Budget = model.Budget;

            if (model.ProjectManagerId != project.ProjectManagerId)
            {
                if (model.ProjectManagerId.HasValue)
                {
                    var pmValid = await _db.Users.AnyAsync(u =>
                        u.Id == model.ProjectManagerId.Value &&
                        u.OrganizationId == orgId &&
                        u.Role == UserRole.ProjectManager &&
                        u.Status == UserStatus.Active);

                    if (!pmValid)
                        return Json(new { success = false, message = "Invalid project manager." });
                }
                project.ProjectManagerId = model.ProjectManagerId;
            }

            // ❌ We do NOT touch project.Status or project.Progress here.
            // Status is changed via the 3-dot menu; Progress is derived from tasks.

            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Project details updated." });
        }

        // ============================================
        // POST: /Projects/UpdateProject
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(Guid id, string status)
        {
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == CurrentOrgId);

            if (project == null)
                return Json(new { success = false, message = "Project not found." });

            if (!Enum.TryParse<ProjectStatus>(status, out var newStatus))
                return Json(new { success = false, message = "Invalid status." });

            var userId = CurrentUserId;
            var isOwnerPm = IsPM && project.ProjectManagerId == userId;

            // ─────────────────────────────────────────────
            // 1) → DONE : PM (owner) or Exec/Admin, only if all tasks are Completed
            // ─────────────────────────────────────────────
            if (newStatus == ProjectStatus.Done)
            {
                if (!isOwnerPm && !IsExecutive && !IsAdmin)
                    return Json(new { success = false, message = "Only the Project Manager or higher can mark a project Done." });

                var total = await _db.Tasks.CountAsync(t => t.ProjectId == id);
                var completed = await _db.Tasks.CountAsync(t =>
                    t.ProjectId == id &&
                    t.Status == Project_Management_System.Models.TaskStatus.Completed);

                if (total == 0)
                    return Json(new { success = false, message = "The project has no tasks yet." });

                if (total != completed)
                    return Json(new { success = false, message = $"All tasks must be Completed first ({completed}/{total})." });

                project.Status = ProjectStatus.Done;
                project.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = "Project marked Done. Awaiting Executive Manager confirmation." });
            }

            // ─────────────────────────────────────────────
            // 2) → COMPLETED : Exec/Admin only, only if status == Done
            // ─────────────────────────────────────────────
            if (newStatus == ProjectStatus.Completed)
            {
                if (!IsExecutive && !IsAdmin)
                    return Json(new { success = false, message = "Only an Executive Manager or System Admin can confirm completion." });

                if (project.Status != ProjectStatus.Done)
                    return Json(new { success = false, message = "The project must first be marked Done by its Project Manager." });

                project.Status = ProjectStatus.Completed;
                project.Progress = 100;
                project.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = "Project Completed." });
            }

            // ─────────────────────────────────────────────
            // 3) Planning / Active / AtRisk / OnHold → Exec/Admin only
            // ─────────────────────────────────────────────
            if (!IsExecutive && !IsAdmin)
                return Json(new { success = false, message = "Only Executive Managers and System Admins can change this status." });

            project.Status = newStatus;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"Status changed to {newStatus}." });
        }

        // ============================================
        // POST: /Projects/UpdateProgress
        // Quick inline progress update (from card)
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress(Guid id, int progress, string? status)
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);

            if (project == null)
                return Json(new { success = false, message = "Project not found." });

            // Permissions: Executive/Admin OR assigned PM
            var canEdit = IsExecutive || IsAdmin
                       || (IsPM && project.ProjectManagerId == userId);

            if (!canEdit)
                return Json(new { success = false, message = "Permission denied." });

            project.Progress = Math.Clamp(progress, 0, 100);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ProjectStatus>(status, out var s))
            {
                project.Status = s;
                if (s == ProjectStatus.Completed) project.Progress = 100;
            }

            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Progress updated." });
        }

        // ============================================
        // POST: /Projects/DeleteProject
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ExecutiveManager,SystemAdmin")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            var orgId = CurrentOrgId;

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);

            if (project == null)
                return Json(new { success = false, message = "Project not found." });

            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"Project '{project.Name}' deleted." });
        }

        // ============================================
        // GET: /Projects/Assign
        // Shows unassigned projects + PMs of the org
        // ============================================
        [Authorize(Roles = "ExecutiveManager,SystemAdmin")]
        public async Task<IActionResult> Assign()
        {
            var orgId = CurrentOrgId;

            // ── Unassigned projects (no PM yet) ──
            var unassigned = await _db.Projects
                .Where(p => p.OrganizationId == orgId && p.ProjectManagerId == null)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Client,
                    p.Domain,
                    p.Priority,
                    p.Status
                })
                .ToListAsync();

            // ── Project Managers in this org ──
            var managers = await _db.Users
                .Where(u => u.OrganizationId == orgId
                         && u.Role == UserRole.ProjectManager
                         && u.Status == UserStatus.Active)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.ProfilePictureUrl
                })
                .ToListAsync();

            // ── Projects assigned to each PM (for the right panel) ──
            var assignedProjectsRaw = await _db.Projects
                .Where(p => p.OrganizationId == orgId && p.ProjectManagerId != null)
                .Select(p => new
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    ManagerId = p.ProjectManagerId!.Value
                })
                .ToListAsync();

            // Build a dictionary of Guid → List<dynamic>
            var assignedMap = new Dictionary<Guid, List<dynamic>>();

            foreach (var group in assignedProjectsRaw.GroupBy(p => p.ManagerId))
            {
                assignedMap[group.Key] = group
                    .Select(p => (dynamic)new { Id = p.ProjectId, Name = p.ProjectName })
                    .ToList();
            }

            ViewBag.Unassigned = unassigned;
            ViewBag.Managers = managers;
            ViewBag.AssignedMap = assignedMap;

            return View();
        }

        // ============================================
        // POST: /Projects/AssignProject
        // Actually assigns a project to a PM
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ExecutiveManager,SystemAdmin")]
        public async Task<IActionResult> AssignProject(Guid projectId, Guid managerId)
        {
            var orgId = CurrentOrgId;

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == orgId);

            if (project == null)
                return Json(new { success = false, message = "Project not found." });

            var manager = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == managerId
                                        && u.OrganizationId == orgId
                                        && u.Role == UserRole.ProjectManager
                                        && u.Status == UserStatus.Active);

            if (manager == null)
                return Json(new { success = false, message = "Invalid project manager." });

            project.ProjectManagerId = managerId;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"'{project.Name}' assigned to {manager.FullName}.",
                projectId = project.Id,
                managerId = manager.Id
            });
        }

        // ============================================
        // POST: /Projects/UnassignProject
        // Removes a PM from a project
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ExecutiveManager,SystemAdmin")]
        public async Task<IActionResult> UnassignProject(Guid projectId)
        {
            var orgId = CurrentOrgId;

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == orgId);

            if (project == null)
                return Json(new { success = false, message = "Project not found." });

            project.ProjectManagerId = null;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Project unassigned." });
        }
    }
}