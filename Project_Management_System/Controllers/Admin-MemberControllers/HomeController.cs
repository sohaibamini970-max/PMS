using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Management_System.Data;
using Project_Management_System.Models;
using Project_Management_System.ViewModels;
using System.Security.Claims;
using AppTaskStatus = Project_Management_System.Models.TaskStatus;

namespace Project_Management_System.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        public HomeController(AppDbContext db) => _db = db;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private Guid CurrentOrgId => Guid.Parse(User.FindFirst("OrganizationId")!.Value);
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        private string CurrentName => User.FindFirst("FullName")?.Value
                                      ?? User.Identity?.Name
                                      ?? "there";

        // ============================================
        // GET: /Home/Index
        // ============================================
        public async Task<IActionResult> Index()
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;
            var role = CurrentRole;

            var isExecOrAdmin = role == "ExecutiveManager" || role == "SystemAdmin";
            var isPM = role == "ProjectManager";
            var isMember = role == "Member";

            // ?? Teams the user leads ??
            var leadingTeamIds = isMember
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

            // ?? Scope the project query ??
            IQueryable<Project> projectQuery = _db.Projects.Where(p => p.OrganizationId == orgId);

            if (isPM)
            {
                projectQuery = projectQuery.Where(p => p.ProjectManagerId == userId);
            }
            else if (isMember)
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
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Client,
                    p.Status,
                    p.Deadline,
                    p.Progress,
                    p.UpdatedAt,
                    ManagerName = p.ProjectManager != null ? p.ProjectManager.FullName : null
                })
                .ToListAsync();

            var projectIds = projectRows.Select(p => p.Id).ToList();

            // ?? Scope the task query ??
            IQueryable<TaskItem> taskQuery = _db.Tasks
                .Where(t => t.OrganizationId == orgId && projectIds.Contains(t.ProjectId));

            if (isMember && !leadingTeamIds.Any())
            {
                taskQuery = taskQuery.Where(t => t.AssigneeId == userId);
            }

            var now = DateTime.UtcNow;
            var taskRows = await taskQuery
                .Select(t => new
                {
                    t.Id,
                    t.ProjectId,
                    t.Title,
                    t.Status,
                    t.Deadline
                })
                .ToListAsync();

            // ?? Build the ViewModel ??
            var vm = new DashboardViewModel
            {
                UserFullName = CurrentName,
                Role = role,
                TotalProjects = projectRows.Count,
                ActiveProjects = projectRows.Count(p => p.Status == ProjectStatus.Active),
                CompletedProjects = projectRows.Count(p => p.Status == ProjectStatus.Completed),
                OverdueProjects = projectRows.Count(p =>
                    p.Deadline.HasValue &&
                    p.Deadline.Value.Date < now.Date &&
                    p.Status != ProjectStatus.Completed &&
                    p.Status != ProjectStatus.Archived),

                TotalTasks = taskRows.Count,
                CompletedTasks = taskRows.Count(t => t.Status == AppTaskStatus.Completed),
                InProgressTasks = taskRows.Count(t => t.Status == AppTaskStatus.InProgress),
                OverdueTasks = taskRows.Count(t =>
                    t.Deadline.HasValue &&
                    t.Deadline.Value.Date < now.Date &&
                    t.Status != AppTaskStatus.Completed),

                OverallCompletion = projectRows.Count == 0
                    ? 0
                    : (int)Math.Round(projectRows.Average(p => (double)p.Progress))
            };

            // ?? Chart data ??
            vm.ChartProjects = projectRows
                .OrderByDescending(p => p.UpdatedAt)
                .Take(8)
                .Select(p => new DashboardProjectItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Percent = p.Progress,
                    Status = p.Status.ToString(),
                    Manager = p.ManagerName ?? "Unassigned",
                    Budget = "—",
                    Desc = ""
                })
                .ToList();

            // ?? Recent projects ??
            vm.RecentProjects = projectRows
                .OrderByDescending(p => p.UpdatedAt)
                .Take(5)
                .Select(p => new RecentProjectItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Initials = p.Name.Length >= 2
                        ? p.Name.Substring(0, 2).ToUpper()
                        : p.Name.Substring(0, 1).ToUpper(),
                    Client = p.Client ?? "—",
                    Deadline = p.Deadline,
                    Progress = p.Progress,
                    Status = p.Status.ToString()
                })
                .ToList();

            // ?? Upcoming deadlines ??
            vm.UpcomingDeadlines = taskRows
                .Where(t => t.Deadline.HasValue
                         && t.Deadline.Value.Date >= now.Date
                         && t.Status != AppTaskStatus.Completed)
                .OrderBy(t => t.Deadline)
                .Take(5)
                .Select(t => new UpcomingDeadlineItem
                {
                    Title = t.Title,
                    When = t.Deadline!.Value.ToString("MMM dd, yyyy"),
                    Color = t.Deadline.Value.Date == now.Date ? "red"
                          : t.Deadline.Value.Date <= now.Date.AddDays(3) ? "amber"
                          : "blue"
                })
                .ToList();

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}