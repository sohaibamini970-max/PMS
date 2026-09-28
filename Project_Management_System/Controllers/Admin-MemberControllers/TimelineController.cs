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
    public class TimelineController : Controller
    {
        private readonly AppDbContext _db;
        public TimelineController(AppDbContext db) => _db = db;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private Guid CurrentOrgId => Guid.Parse(User.FindFirst("OrganizationId")!.Value);
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        private bool IsExec => CurrentRole == "ExecutiveManager";
        private bool IsAdmin => CurrentRole == "SystemAdmin";
        private bool IsPM => CurrentRole == "ProjectManager";
        private bool IsMember => CurrentRole == "Member";

        public async Task<IActionResult> Index()
        {
            // Members are not allowed on the Timeline page
            if (IsMember)
                return RedirectToAction("Index", "Home");

            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            // ── Project scope ──
            IQueryable<Project> projectQuery = _db.Projects
                .Where(p => p.OrganizationId == orgId);

            if (IsPM)
                projectQuery = projectQuery.Where(p => p.ProjectManagerId == userId);

            var projectRows = await projectQuery
                .OrderByDescending(p => p.StartDate)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Client,
                    p.Status,
                    p.StartDate,
                    p.Deadline
                })
                .ToListAsync();

            var projectIds = projectRows.Select(p => p.Id).ToList();

            // ── Task rows ──
            var taskRows = await _db.Tasks
                .Where(t => projectIds.Contains(t.ProjectId))
                .OrderBy(t => t.StartDate)
                .Select(t => new
                {
                    t.Id,
                    t.ProjectId,
                    t.Title,
                    t.Status,
                    t.StartDate,
                    t.Deadline,
                    t.SubmittedAt,
                    t.CompletedAt
                })
                .ToListAsync();

            var today = DateTime.UtcNow.Date;
            var tasksByProject = taskRows
                .GroupBy(t => t.ProjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var projects = new List<TimelineProjectItem>();

            foreach (var p in projectRows)
            {
                var pStart = p.StartDate?.Date;
                var pEnd = p.Deadline?.Date;
                var projectDays = (pStart.HasValue && pEnd.HasValue)
                    ? Math.Max(1, (int)(pEnd.Value - pStart.Value).TotalDays + 1)
                    : 0;

                //var projTasks = tasksByProject.TryGetValue(p.Id, out var ts)
                //    ? ts
                //    : new List<dynamic>().Select(x => x).ToList(); // placeholder

                var taskItems = new List<TimelineTaskItem>();

                if (tasksByProject.TryGetValue(p.Id, out var rawTasks))
                {
                    foreach (var t in rawTasks)
                    {
                        var tStart = t.StartDate?.Date;
                        var tEnd = t.Deadline?.Date;

                        // ── Status classification ──
                        string statusKey;
                        string statusNote;
                        if (t.Status == AppTaskStatus.Completed)
                        {
                            statusKey = "Completed";
                            statusNote = t.CompletedAt.HasValue
                                ? $"✓ Done on {t.CompletedAt.Value:MMM dd}"
                                : "✓ Completed";
                        }
                        else if (t.Deadline.HasValue && t.Deadline.Value.Date < today)
                        {
                            statusKey = "Overdue";
                            var daysLate = (today - t.Deadline.Value.Date).Days;
                            statusNote = $"⚠ Overdue by {daysLate} day{(daysLate == 1 ? "" : "s")}";
                        }
                        else
                        {
                            statusKey = "Pending";
                            statusNote = t.Status == AppTaskStatus.InProgress
                                ? "⏳ In progress"
                                : "Pending";
                        }

                        // ── Compute bar position (relative to project span) ──
                        double leftPct = 0;
                        double widthPct = 100;
                        if (pStart.HasValue && pEnd.HasValue && tStart.HasValue && tEnd.HasValue)
                        {
                            var totalDays = (pEnd.Value - pStart.Value).TotalDays;
                            if (totalDays > 0)
                            {
                                var startOffset = (tStart.Value - pStart.Value).TotalDays;
                                var taskSpan = (tEnd.Value - tStart.Value).TotalDays;

                                leftPct = Math.Max(0, Math.Min(100, (startOffset / totalDays) * 100));
                                widthPct = Math.Max(2, Math.Min(100 - leftPct, (taskSpan / totalDays) * 100));
                            }
                        }

                        var taskDays = (tStart.HasValue && tEnd.HasValue)
                            ? Math.Max(1, (int)(tEnd.Value - tStart.Value).TotalDays + 1)
                            : 0;

                        var dateRange = (tStart.HasValue && tEnd.HasValue)
                            ? $"{tStart.Value:MMM dd} → {tEnd.Value:MMM dd}"
                            : (tStart.HasValue ? $"{tStart.Value:MMM dd} →" :
                               tEnd.HasValue ? $"→ {tEnd.Value:MMM dd}" : "No dates");

                        taskItems.Add(new TimelineTaskItem
                        {
                            Id = t.Id,
                            Code = "TSK-" + t.Id.ToString().Substring(0, 4).ToUpper(),
                            Title = t.Title,
                            Status = statusKey,
                            StartDate = t.StartDate,
                            Deadline = t.Deadline,
                            DateRangeLabel = dateRange,
                            StatusNote = statusNote,
                            LeftPercent = leftPct,
                            WidthPercent = widthPct,
                            DurationLabel = taskDays > 0 ? $"{taskDays} day{(taskDays == 1 ? "" : "s")}" : ""
                        });
                    }
                }

                var spanLabel = (pStart.HasValue && pEnd.HasValue)
                    ? $"{pStart.Value:MMM dd, yyyy} → {pEnd.Value:MMM dd, yyyy} ({projectDays} days)"
                    : "No timeline set";

                projects.Add(new TimelineProjectItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Client = p.Client ?? "—",
                    Initials = p.Name.Length >= 2
                        ? p.Name.Substring(0, 2).ToUpper()
                        : p.Name.Substring(0, 1).ToUpper(),
                    Status = p.Status.ToString(),
                    StartDate = p.StartDate,
                    Deadline = p.Deadline,
                    ProjectSpanDays = projectDays,
                    ProjectSpanLabel = spanLabel,
                    CompletedCount = taskItems.Count(i => i.Status == "Completed"),
                    PendingCount = taskItems.Count(i => i.Status == "Pending"),
                    OverdueCount = taskItems.Count(i => i.Status == "Overdue"),
                    Tasks = taskItems
                });
            }

            var vm = new TimelineViewModel
            {
                TotalProjects = projects.Count,
                TotalTasks = projects.Sum(p => p.Tasks.Count),
                CompletedTasks = projects.Sum(p => p.CompletedCount),
                PendingTasks = projects.Sum(p => p.PendingCount),
                OverdueTasks = projects.Sum(p => p.OverdueCount),
                Projects = projects
            };

            return View(vm);
        }
    }
}