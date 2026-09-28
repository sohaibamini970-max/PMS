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
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        public ProfileController(AppDbContext db) => _db = db;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private Guid CurrentOrgId => Guid.Parse(User.FindFirst("OrganizationId")!.Value);
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

        public async Task<IActionResult> Index()
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return NotFound();

            var isExec = CurrentRole == "ExecutiveManager";
            var isAdmin = CurrentRole == "SystemAdmin";
            var isPM = CurrentRole == "ProjectManager";
            var isMember = CurrentRole == "Member";

            var vm = new ProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString(),
                ProfilePictureUrl = user.ProfilePictureUrl,
                Phone = user.Phone,
                CreatedAt = user.CreatedAt
            };

            vm.RoleDisplay = DisplayRole(vm.Role);
            vm.ProfileRoleBadge = vm.RoleDisplay;

            // ── Teams this user leads (only relevant for Members) ──
            var leadingTeamIds = isMember
                ? await _db.Teams
                    .Where(t => t.OrganizationId == CurrentOrgId && t.LeaderId == CurrentUserId)
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

            // ── Project scope ──
            IQueryable<Project> projectQuery = _db.Projects.Where(p => p.OrganizationId == CurrentOrgId);

            if (isPM)
            {
                projectQuery = projectQuery.Where(p => p.ProjectManagerId == CurrentUserId);
            }
            else if (isMember)
            {
                projectQuery = projectQuery.Where(p =>
                    p.AssignedMemberId == CurrentUserId ||
                    (p.AssignedTeamId != null && leadingTeamIds.Contains(p.AssignedTeamId.Value)) ||
                    _db.Tasks.Any(t => t.ProjectId == p.Id &&
                        (t.AssigneeId == CurrentUserId ||
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
                    p.Progress,
                    p.Deadline,
                    p.UpdatedAt
                })
                .ToListAsync();

            var projectIds = projectRows.Select(p => p.Id).ToList();

            // ── Task scope ──
            IQueryable<TaskItem> taskQuery = _db.Tasks
                .Where(t => t.OrganizationId == CurrentOrgId && projectIds.Contains(t.ProjectId));

            if (isMember && !leadingTeamIds.Any())
            {
                taskQuery = taskQuery.Where(t => t.AssigneeId == CurrentUserId);
            }

            var today = DateTime.UtcNow.Date;

            var taskRows = await taskQuery
                .Select(t => new
                {
                    t.Id,
                    t.ProjectId,
                    t.Title,
                    t.Priority,
                    t.Status,
                    t.Deadline,
                    t.CreatedAt,
                    t.CompletedAt,
                    ProjectName = t.Project != null ? t.Project.Name : ""
                })
                .ToListAsync();

            // ── KPI computations ──
            vm.CompletedTasks = taskRows.Count(t => t.Status == AppTaskStatus.Completed);
            vm.OverdueTasks = taskRows.Count(t =>
                t.Status != AppTaskStatus.Completed &&
                t.Deadline.HasValue && t.Deadline.Value.Date < today);
            vm.PendingTasks = taskRows.Count - vm.CompletedTasks - vm.OverdueTasks;
            if (vm.PendingTasks < 0) vm.PendingTasks = 0;

            vm.TotalTasks = taskRows.Count;
            vm.TotalProjects = projectRows.Count;

            // ── Team count (role-aware) ──
            if (isMember)
            {
                vm.TotalTeams = await _db.TeamMembers
                    .Where(tm => tm.UserId == CurrentUserId)
                    .Select(tm => tm.TeamId)
                    .Distinct()
                    .CountAsync();
            }
            else if (isPM && projectIds.Any())
            {
                vm.TotalTeams = await _db.Teams
                    .Where(t => t.OrganizationId == CurrentOrgId &&
                                t.Tasks.Any(tt => projectIds.Contains(tt.ProjectId)))
                    .CountAsync();
            }
            else
            {
                vm.TotalTeams = await _db.Teams
                    .Where(t => t.OrganizationId == CurrentOrgId)
                    .CountAsync();
            }

            vm.SuccessRate = vm.TotalTasks == 0
                ? 0
                : (int)Math.Round((double)vm.CompletedTasks / vm.TotalTasks * 100);

            // ── Section titles ──
            if (isExec || isAdmin)
            {
                vm.SidePanelTitle = "Organization";
                vm.ProjectsSectionLabel = "(All Organization)";
                vm.RecentTasksTitle = "Recent Tasks Across Organization";
            }
            else if (isPM)
            {
                vm.SidePanelTitle = "Team Overview";
                vm.ProjectsSectionLabel = "(Managed)";
                vm.RecentTasksTitle = "Recent Tasks (Approved & Pending)";
            }
            else
            {
                vm.SidePanelTitle = "My Teams";
                vm.ProjectsSectionLabel = "(Assigned to me)";
                vm.RecentTasksTitle = "My Recent Tasks";
            }

            // ── Side stat cards ──
            vm.SideStats = await BuildSideStats(isExec, isAdmin, isPM, isMember, projectIds, taskRows, vm);

            // ── Monthly chart (last 6 months) ──
            var now = DateTime.UtcNow;
            var firstOfThisMonth = new DateTime(now.Year, now.Month, 1);

            for (int i = 5; i >= 0; i--)
            {
                var monthStart = firstOfThisMonth.AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);

                vm.MonthlyLabels.Add(monthStart.ToString("MMM"));

                vm.MonthlyCompleted.Add(taskRows.Count(t =>
                    t.Status == AppTaskStatus.Completed &&
                    t.CompletedAt.HasValue &&
                    t.CompletedAt.Value >= monthStart &&
                    t.CompletedAt.Value < monthEnd));

                vm.MonthlyPending.Add(taskRows.Count(t =>
                    t.Status != AppTaskStatus.Completed &&
                    t.CreatedAt >= monthStart &&
                    t.CreatedAt < monthEnd));
            }

            // ── Projects list (top 5 by progress) ──
            var projectTaskCounts = taskRows
                .GroupBy(t => t.ProjectId)
                .ToDictionary(g => g.Key, g => g.Count());

            vm.Projects = projectRows
                .OrderByDescending(p => p.Progress)
                .Take(5)
                .Select(p => new ProfileProjectItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Client = p.Client ?? "—",
                    Status = p.Status.ToString(),
                    Progress = p.Progress,
                    TaskCount = projectTaskCounts.GetValueOrDefault(p.Id, 0),
                    Deadline = p.Deadline
                })
                .ToList();

            // ── Recent tasks (top 6) ──
            vm.RecentTasks = taskRows
                .OrderByDescending(t => t.CreatedAt)
                .Take(6)
                .Select(t => new ProfileTaskItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    ProjectName = t.ProjectName,
                    Priority = t.Priority.ToString(),
                    Deadline = t.Deadline,
                    Status = ComputeTaskStatus(t.Status, t.Deadline, today)
                })
                .ToList();

            return View(vm);
        }

        // ============================================
        // HELPERS
        // ============================================
        private static string DisplayRole(string role) => role switch
        {
            "SuperAdmin" => "Platform Owner",
            "SystemAdmin" => "System Administrator",
            "ExecutiveManager" => "Executive Manager",
            "ProjectManager" => "Project Manager",
            "Member" => "Member",
            _ => role
        };

        private static string ComputeTaskStatus(AppTaskStatus status, DateTime? deadline, DateTime today)
        {
            if (status == AppTaskStatus.Completed) return "Completed";
            if (deadline.HasValue && deadline.Value.Date < today) return "Overdue";
            if (status == AppTaskStatus.InProgress) return "InProgress";
            if (status == AppTaskStatus.Done) return "Done";
            if (status == AppTaskStatus.Planning) return "Planning";
            return "Pending";
        }

        private async Task<List<ProfileStatCard>> BuildSideStats(
            bool isExec, bool isAdmin, bool isPM, bool isMember,
            List<Guid> projectIds,
            IEnumerable<dynamic> taskRows,
            ProfileViewModel vm)
        {
            var cards = new List<ProfileStatCard>();

            if (isExec || isAdmin)
            {
                var totalOrgProjects = await _db.Projects
                    .CountAsync(p => p.OrganizationId == CurrentOrgId);
                var totalUsers = await _db.Users
                    .CountAsync(u => u.OrganizationId == CurrentOrgId && u.Status == UserStatus.Active);
                var completedProjects = await _db.Projects
                    .CountAsync(p => p.OrganizationId == CurrentOrgId && p.Status == ProjectStatus.Completed);

                cards.Add(new ProfileStatCard { Label = "Total Organization", Value = totalOrgProjects.ToString(), Subtitle = "Projects being tracked", Color = "blue" });
                cards.Add(new ProfileStatCard { Label = "Teams Managed", Value = vm.TotalTeams.ToString(), Subtitle = "Across the org", Color = "emerald" });
                cards.Add(new ProfileStatCard { Label = "People", Value = totalUsers.ToString(), Subtitle = "Active team members", Color = "purple" });
                cards.Add(new ProfileStatCard { Label = "Overall Success Rate", Value = (totalOrgProjects == 0 ? 0 : (int)Math.Round((double)completedProjects / totalOrgProjects * 100)) + "%", Subtitle = "Project completion", Color = "amber" });
            }
            else if (isPM)
            {
                var teamSize = await _db.Users
                    .Where(u => u.OrganizationId == CurrentOrgId &&
                                u.Role == UserRole.Member &&
                                u.Status == UserStatus.Active &&
                                _db.Tasks.Any(t => projectIds.Contains(t.ProjectId) && t.AssigneeId == u.Id))
                    .CountAsync();

                var approvedThisMonth = await _db.Tasks
                    .CountAsync(t => projectIds.Contains(t.ProjectId) &&
                                     t.Status == AppTaskStatus.Completed &&
                                     t.CompletedAt.HasValue &&
                                     t.CompletedAt.Value.Month == DateTime.UtcNow.Month &&
                                     t.CompletedAt.Value.Year == DateTime.UtcNow.Year);

                cards.Add(new ProfileStatCard { Label = "Projects Managed", Value = vm.TotalProjects.ToString(), Subtitle = "Active engagements", Color = "blue" });
                cards.Add(new ProfileStatCard { Label = "Team Size", Value = teamSize.ToString(), Subtitle = "Members reporting", Color = "emerald" });
                cards.Add(new ProfileStatCard { Label = "This Month", Value = approvedThisMonth.ToString(), Subtitle = "Tasks approved", Color = "amber" });
            }
            else // Member
            {
                cards.Add(new ProfileStatCard { Label = "My Tasks", Value = vm.PendingTasks.ToString(), Subtitle = "Currently assigned", Color = "emerald" });
                cards.Add(new ProfileStatCard { Label = "My Teams", Value = vm.TotalTeams.ToString(), Subtitle = "Teams I belong to", Color = "blue" });
                cards.Add(new ProfileStatCard { Label = "Done", Value = vm.CompletedTasks.ToString(), Subtitle = "Tasks I've finished", Color = "purple" });
            }

            return cards;
        }
    }
}