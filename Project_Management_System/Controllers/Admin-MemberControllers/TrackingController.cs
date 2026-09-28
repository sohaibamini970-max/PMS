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
    public class TrackingController : Controller
    {
        private readonly AppDbContext _db;
        public TrackingController(AppDbContext db) => _db = db;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private Guid CurrentOrgId => Guid.Parse(User.FindFirst("OrganizationId")!.Value);
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        private bool IsExec => CurrentRole == "ExecutiveManager";
        private bool IsAdmin => CurrentRole == "SystemAdmin";
        private bool IsPM => CurrentRole == "ProjectManager";
        private bool IsMember => CurrentRole == "Member";

        public async Task<IActionResult> Index()
        {
            // Members can't view tracking
            if (IsMember)
                return RedirectToAction("Index", "Home");

            var orgId = CurrentOrgId;
            var userId = CurrentUserId;
            var today = DateTime.UtcNow.Date;

            // ── Projects in scope ──
            IQueryable<Project> projectQuery = _db.Projects.Where(p => p.OrganizationId == orgId);
            if (IsPM)
                projectQuery = projectQuery.Where(p => p.ProjectManagerId == userId);

            var projectRows = await projectQuery
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            var projectIds = projectRows.Select(p => p.Id).ToList();
            var projectNameById = projectRows.ToDictionary(p => p.Id, p => p.Name);

            // ── All tasks in scope ──
            var tasks = await _db.Tasks
                .Where(t => t.OrganizationId == orgId && projectIds.Contains(t.ProjectId))
                .Select(t => new
                {
                    t.Id,
                    t.ProjectId,
                    t.Status,
                    t.Deadline,
                    t.AssigneeId,
                    t.AssigneeTeamId
                })
                .ToListAsync();

            // Classify → Completed / Pending / Overdue
            string Classify(AppTaskStatus status, DateTime? deadline)
            {
                if (status == AppTaskStatus.Completed) return "Completed";
                if (deadline.HasValue && deadline.Value.Date < today) return "Overdue";
                return "Pending";
            }

            var classified = tasks.Select(t => new
            {
                t.Id,
                t.ProjectId,
                Class = Classify(t.Status, t.Deadline),
                t.AssigneeId,
                t.AssigneeTeamId
            }).ToList();

            // Summary
            var vm = new TrackingViewModel
            {
                TotalProjects = projectRows.Count,
                TotalCompletedTasks = classified.Count(t => t.Class == "Completed"),
                TotalPendingTasks = classified.Count(t => t.Class == "Pending"),
                TotalOverdueTasks = classified.Count(t => t.Class == "Overdue")
            };

            // ── TEAMS ──
            IQueryable<Team> teamQuery = _db.Teams.Where(t => t.OrganizationId == orgId);

            if (IsPM)
            {
                var teamIdsFromTasks = classified
                    .Where(t => t.AssigneeTeamId.HasValue)
                    .Select(t => t.AssigneeTeamId!.Value)
                    .Distinct()
                    .ToList();

                var memberIdsWithTasks = classified
                    .Where(t => t.AssigneeId.HasValue)
                    .Select(t => t.AssigneeId!.Value)
                    .Distinct()
                    .ToList();

                var teamsViaMembers = await _db.TeamMembers
                    .Where(tm => memberIdsWithTasks.Contains(tm.UserId))
                    .Select(tm => tm.TeamId)
                    .Distinct()
                    .ToListAsync();

                var combinedIds = teamIdsFromTasks.Concat(teamsViaMembers).Distinct().ToList();
                teamQuery = teamQuery.Where(t => combinedIds.Contains(t.Id));
            }

            var teamRows = await teamQuery
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Color,
                    MemberIds = t.Members.Select(m => m.UserId).ToList()
                })
                .ToListAsync();

            var teamsTracking = new List<TeamTrackingItem>();

            foreach (var team in teamRows)
            {
                var teamTasks = classified.Where(t =>
                    (t.AssigneeTeamId.HasValue && t.AssigneeTeamId.Value == team.Id) ||
                    (t.AssigneeId.HasValue && team.MemberIds.Contains(t.AssigneeId.Value))
                ).ToList();

                var completed = teamTasks.Count(t => t.Class == "Completed");
                var pending = teamTasks.Count(t => t.Class == "Pending");
                var overdue = teamTasks.Count(t => t.Class == "Overdue");
                var total = completed + pending + overdue;
                var rate = total == 0 ? 0 : (int)Math.Round((double)completed / total * 100);

                var byProject = teamTasks
                    .GroupBy(t => t.ProjectId)
                    .Select(g => new ProjectBreakdownItem
                    {
                        Id = g.Key,
                        Name = projectNameById.GetValueOrDefault(g.Key, "—"),
                        Completed = g.Count(t => t.Class == "Completed"),
                        Pending = g.Count(t => t.Class == "Pending"),
                        Overdue = g.Count(t => t.Class == "Overdue"),
                        CompletionRate = g.Count() == 0 ? 0
                            : (int)Math.Round((double)g.Count(t => t.Class == "Completed") / g.Count() * 100)
                    })
                    .ToList();

                teamsTracking.Add(new TeamTrackingItem
                {
                    Id = team.Id,
                    Name = team.Name,
                    Color = team.Color,
                    MemberCount = team.MemberIds.Count,
                    ProjectCount = byProject.Count,
                    CompletedTasks = completed,
                    PendingTasks = pending,
                    OverdueTasks = overdue,
                    CompletionRate = rate,
                    Projects = byProject
                });
            }

            teamsTracking = teamsTracking
                .OrderByDescending(t => t.CompletionRate)
                .ThenByDescending(t => t.CompletedTasks)
                .ToList();

            for (int i = 0; i < teamsTracking.Count && i < 3; i++)
            {
                teamsTracking[i].RankMedal = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => ""
                };
            }

            vm.Teams = teamsTracking;

            // ── MEMBERS ──
            var memberIdsWithTasksAll = classified
                .Where(t => t.AssigneeId.HasValue)
                .Select(t => t.AssigneeId!.Value)
                .Distinct()
                .ToList();

            IQueryable<User> memberQuery = _db.Users
                .Where(u => u.OrganizationId == orgId
                         && u.Status == UserStatus.Active
                         && u.Role == UserRole.Member);

            if (IsPM)
                memberQuery = memberQuery.Where(u => memberIdsWithTasksAll.Contains(u.Id));

            var memberRows = await memberQuery
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.ProfilePictureUrl,
                    u.Role
                })
                .ToListAsync();

            var membersTracking = new List<MemberTrackingItem>();

            foreach (var m in memberRows)
            {
                var memberTasks = classified
                    .Where(t => t.AssigneeId.HasValue && t.AssigneeId.Value == m.Id)
                    .ToList();

                if (memberTasks.Count == 0) continue;

                var completed = memberTasks.Count(t => t.Class == "Completed");
                var pending = memberTasks.Count(t => t.Class == "Pending");
                var overdue = memberTasks.Count(t => t.Class == "Overdue");
                var total = completed + pending + overdue;
                var rate = total == 0 ? 0 : (int)Math.Round((double)completed / total * 100);

                var byProject = memberTasks
                    .GroupBy(t => t.ProjectId)
                    .Select(g => new ProjectBreakdownItem
                    {
                        Id = g.Key,
                        Name = projectNameById.GetValueOrDefault(g.Key, "—"),
                        Completed = g.Count(t => t.Class == "Completed"),
                        Pending = g.Count(t => t.Class == "Pending"),
                        Overdue = g.Count(t => t.Class == "Overdue"),
                        CompletionRate = g.Count() == 0 ? 0
                            : (int)Math.Round((double)g.Count(t => t.Class == "Completed") / g.Count() * 100)
                    })
                    .ToList();

                membersTracking.Add(new MemberTrackingItem
                {
                    Id = m.Id,
                    FullName = m.FullName,
                    Email = m.Email,
                    ProfilePictureUrl = m.ProfilePictureUrl,
                    Role = m.Role.ToString(),
                    ProjectCount = byProject.Count,
                    CompletedTasks = completed,
                    PendingTasks = pending,
                    OverdueTasks = overdue,
                    CompletionRate = rate,
                    Projects = byProject
                });
            }

            membersTracking = membersTracking
                .OrderByDescending(m => m.CompletionRate)
                .ThenByDescending(m => m.CompletedTasks)
                .ToList();

            for (int i = 0; i < membersTracking.Count && i < 3; i++)
            {
                membersTracking[i].RankMedal = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => ""
                };
            }

            vm.Members = membersTracking;

            return View(vm);
        }
    }
}