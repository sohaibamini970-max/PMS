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
    public class TasksController : Controller
    {
        private readonly AppDbContext _db;
        public TasksController(AppDbContext db) => _db = db;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private Guid CurrentOrgId => Guid.Parse(User.FindFirst("OrganizationId")!.Value);
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        private bool IsExecutive => CurrentRole == "ExecutiveManager";
        private bool IsPM => CurrentRole == "ProjectManager";
        private bool IsMember => CurrentRole == "Member";
        private bool IsAdmin => CurrentRole == "SystemAdmin";

        // ============================================
        // GET: /Tasks
        // ============================================
        public async Task<IActionResult> Index()
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            // ── Is the current user a team leader? ──
            var leadingTeamIds = await _db.Teams
                .Where(t => t.OrganizationId == orgId && t.LeaderId == userId)
                .Select(t => t.Id)
                .ToListAsync();

            var isTeamLeader = leadingTeamIds.Any();

            // ── Members of the teams the user leads ──
            var leaderTeamMemberIds = isTeamLeader
                ? await _db.TeamMembers
                    .Where(tm => leadingTeamIds.Contains(tm.TeamId))
                    .Select(tm => tm.UserId)
                    .Distinct()
                    .ToListAsync()
                : new List<Guid>();

            // ── Project query (role-aware) ──
            IQueryable<Project> projectQuery = _db.Projects.Where(p => p.OrganizationId == orgId);

            if (IsPM)
            {
                projectQuery = projectQuery.Where(p => p.ProjectManagerId == userId);
            }
            else if (IsMember && isTeamLeader)
            {
                // Team leader: all projects touching their team / members / themselves
                projectQuery = projectQuery.Where(p =>
                    (p.AssignedTeamId.HasValue && leadingTeamIds.Contains(p.AssignedTeamId.Value)) ||
                    (p.AssignedMemberId.HasValue && leaderTeamMemberIds.Contains(p.AssignedMemberId.Value)) ||
                    _db.Tasks.Any(t => t.ProjectId == p.Id &&
                        ((t.AssigneeTeamId.HasValue && leadingTeamIds.Contains(t.AssigneeTeamId.Value)) ||
                         (t.AssigneeId.HasValue && leaderTeamMemberIds.Contains(t.AssigneeId.Value)) ||
                         t.AssigneeId == userId)));
            }
            else if (IsMember)
            {
                // Plain member: only projects where they personally have tasks
                projectQuery = projectQuery.Where(p =>
                    _db.Tasks.Any(t => t.ProjectId == p.Id && t.AssigneeId == userId));
            }

            var projects = await projectQuery
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectWithTasksViewModel
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    ProjectClient = p.Client,
                    ProjectStatus = p.Status,
                    ProjectProgress = p.Progress,
                    ProjectInitials = p.Name.Length >= 2
                        ? p.Name.Substring(0, 2).ToUpper()
                        : p.Name.Substring(0, 1).ToUpper(),

                    AssignedTeamId = p.AssignedTeamId,
                    AssignedTeamName = p.AssignedTeam != null ? p.AssignedTeam.Name : null,
                    AssignedMemberId = p.AssignedMemberId,
                    AssignedMemberName = p.AssignedMember != null ? p.AssignedMember.FullName : null,

                    Tasks = _db.Tasks
                        .Where(t => t.ProjectId == p.Id)
                        // Member who is NOT a leader → only their own tasks.
                        // Everyone else (PM / Exec / Admin / Team Leader) → all tasks.
                        .Where(t => !(IsMember && !isTeamLeader) || t.AssigneeId == userId)
                        .OrderByDescending(t => t.CreatedAt)
                        .Select(t => new TaskListItemViewModel
                        {
                            Id = t.Id,
                            ProjectId = t.ProjectId,
                            Title = t.Title,
                            Description = t.Description,
                            Objectives = t.Objectives,
                            InstructionsText = t.InstructionsText,
                            InstructionsFileUrl = t.InstructionsFileUrl,
                            StartDate = t.StartDate,
                            Deadline = t.Deadline,
                            Priority = t.Priority,
                            Status = t.Status,
                            AssigneeId = t.AssigneeId,
                            AssigneeName = t.Assignee != null ? t.Assignee.FullName : null,
                            AssigneePicture = t.Assignee != null ? t.Assignee.ProfilePictureUrl : null,
                            AssigneeTeamId = t.AssigneeTeamId,
                            AssigneeTeamName = t.AssigneeTeam != null ? t.AssigneeTeam.Name : null,
                            SubmittedAt = t.SubmittedAt,
                            CompletedAt = t.CompletedAt,
                            SubtaskCount = t.Subtasks.Count,
                            SubtaskDone = t.Subtasks.Count(s => s.Status == SubtaskStatus.Done),
                            ChallengeCount = t.Challenges.Count
                        }).ToList()
                }).ToListAsync();

            // ── Members dropdown ──
            var membersQuery = _db.Users
                .Where(u => u.OrganizationId == orgId
                         && u.Role == UserRole.Member
                         && u.Status == UserStatus.Active);

            if (IsMember && !isTeamLeader)
            {
                membersQuery = membersQuery.Where(u => u.Id == userId);
            }
            else if (IsMember && isTeamLeader)
            {
                membersQuery = membersQuery.Where(u =>
                    u.Id == userId || leaderTeamMemberIds.Contains(u.Id));
            }

            var members = await membersQuery
                .OrderBy(u => u.FullName)
                .Select(u => new MemberOptionViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email
                }).ToListAsync();

            var vm = new TasksIndexViewModel
            {
                Projects = projects,
                Members = members,
                CanCreate = IsPM || IsExecutive || IsAdmin || isTeamLeader,
                IsMember = IsMember,
                IsPM = IsPM,
                CurrentRole = CurrentRole
            };

            return View(vm);
        }

        // ============================================
        // GET: /Tasks/GetTask/{id}
        // ============================================
        [HttpGet]
        public async Task<IActionResult> GetTask(Guid id)
        {
            var orgId = CurrentOrgId;
            var t = await _db.Tasks
                .Where(x => x.Id == id && x.OrganizationId == orgId)
                .Include(x => x.Assignee)
                .Include(x => x.Subtasks.OrderBy(s => s.OrderIndex))
                .Include(x => x.Challenges).ThenInclude(c => c.RaisedBy)
                .FirstOrDefaultAsync();

            if (t == null) return NotFound();

            return Json(new
            {
                id = t.Id,
                projectId = t.ProjectId,
                title = t.Title,
                description = t.Description,
                objectives = t.Objectives,
                instructionsText = t.InstructionsText,
                instructionsFileUrl = t.InstructionsFileUrl,
                startDate = t.StartDate?.ToString("yyyy-MM-dd"),
                deadline = t.Deadline?.ToString("yyyy-MM-dd"),
                priority = t.Priority.ToString(),
                status = t.Status.ToString(),
                assigneeId = t.AssigneeId,
                assigneeName = t.Assignee?.FullName,
                assigneePicture = t.Assignee?.ProfilePictureUrl,
                submittedAt = t.SubmittedAt,
                completedAt = t.CompletedAt,
                subtasks = t.Subtasks.Select(s => new {
                    id = s.Id,
                    title = s.Title,
                    description = s.Description,
                    status = s.Status.ToString(),
                    orderIndex = s.OrderIndex
                }),
                challenges = t.Challenges.OrderByDescending(c => c.CreatedAt).Select(c => new {
                    id = c.Id,
                    title = c.Title,
                    description = c.Description,
                    status = c.Status.ToString(),
                    raisedByName = c.RaisedBy != null ? c.RaisedBy.FullName : "",
                    raisedByPicture = c.RaisedBy != null ? c.RaisedBy.ProfilePictureUrl : null,
                    createdAt = c.CreatedAt.ToString("MMM dd, yyyy")
                })
            });
        }

        // ============================================
        // POST: /Tasks/CreateTask
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask(CreateTaskViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill all required fields." });

            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            // Validate project exists and belongs to org
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId && p.OrganizationId == orgId);
            if (project == null) return Json(new { success = false, message = "Project not found." });

            // PM can only create tasks for their own projects
            if (IsPM && project.ProjectManagerId != userId)
                return Json(new { success = false, message = "You can only add tasks to your own projects." });

            // Date validation — must fall within project timeline
            var start = model.StartDate.HasValue
                ? DateTime.SpecifyKind(model.StartDate.Value.Date, DateTimeKind.Utc) : (DateTime?)null;
            var end = model.Deadline.HasValue
                ? DateTime.SpecifyKind(model.Deadline.Value.Date, DateTimeKind.Utc) : (DateTime?)null;

            if (start.HasValue && end.HasValue && end.Value < start.Value)
                return Json(new { success = false, message = "Deadline must be after start date." });

            if (project.StartDate.HasValue && start.HasValue && start.Value < project.StartDate.Value.Date)
                return Json(new { success = false, message = "Task start date cannot be before project start date." });

            if (project.Deadline.HasValue && end.HasValue && end.Value > project.Deadline.Value.Date)
                return Json(new { success = false, message = "Task deadline cannot exceed project deadline." });

            // Validate assignee is a Member in this org
            if (model.AssigneeId.HasValue)
            {
                var ok = await _db.Users.AnyAsync(u =>
                    u.Id == model.AssigneeId.Value &&
                    u.OrganizationId == orgId &&
                    u.Role == UserRole.Member &&
                    u.Status == UserStatus.Active);
                if (!ok) return Json(new { success = false, message = "Invalid assignee — must be an active member of your organization." });
            }

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                ProjectId = model.ProjectId,
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                Objectives = model.Objectives?.Trim(),
                InstructionsText = model.InstructionsText?.Trim(),
                InstructionsFileUrl = model.InstructionsFileUrl?.Trim(),
                StartDate = start,
                Deadline = end,
                Priority = model.Priority,
                Status = AppTaskStatus.ToDo,
                AssigneeId = model.AssigneeId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"Task '{task.Title}' created.", taskId = task.Id });
        }

        // ============================================
        // POST: /Tasks/UpdateStatus
        // Role-aware status change
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(Guid id, string status)
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId);
            if (task == null) return Json(new { success = false, message = "Task not found." });

            if (!Enum.TryParse<AppTaskStatus>(status, out var newStatus))
                return Json(new { success = false, message = "Invalid status." });

            // ─── STATUS TRANSITION RULES ───

            // 1) Only the assignee (Member) can mark a task as Done (submit work).
            //    PM / Executive / Admin cannot mark Done.
            if (newStatus == AppTaskStatus.Done)
            {
                if (!IsMember)
                    return Json(new { success = false, message = "Only the assigned member can submit a task as Done." });

                if (task.AssigneeId != userId)
                    return Json(new { success = false, message = "You can only submit tasks assigned to you." });
            }

            // 2) Only PM / Executive / Admin can mark a task as Completed (approve work).
            //    Member cannot mark Completed.
            if (newStatus == AppTaskStatus.Completed)
            {
                if (IsMember)
                    return Json(new { success = false, message = "Only a Project Manager or higher can mark a task as Completed." });

                // Task must first be Done (submitted by the member)
                if (task.Status != AppTaskStatus.Done)
                    return Json(new { success = false, message = "The task must be submitted (Done) by the assignee before it can be Completed." });

                // PM can only approve tasks in their own projects
                if (IsPM)
                {
                    var project = await _db.Projects.FindAsync(task.ProjectId);
                    if (project?.ProjectManagerId != userId)
                        return Json(new { success = false, message = "You can only update tasks in your own projects." });
                }
            }

            // 3) Member can only modify their own task (applies to any status change)
            if (IsMember && task.AssigneeId != userId)
                return Json(new { success = false, message = "You can only update tasks assigned to you." });

            // 4) PM can only touch tasks in their own projects (applies to non-Completed changes too)
            if (IsPM && newStatus != AppTaskStatus.Completed)
            {
                var project = await _db.Projects.FindAsync(task.ProjectId);
                if (project?.ProjectManagerId != userId)
                    return Json(new { success = false, message = "You can only update tasks in your own projects." });
            }

            // 5) A task can only be marked Done or Completed if at least one subtask is Done
            if (newStatus == AppTaskStatus.Done || newStatus == AppTaskStatus.Completed)
            {
                var hasDoneSubtask = await _db.TaskSubtasks
                    .AnyAsync(s => s.TaskId == task.Id && s.Status == SubtaskStatus.Done);

                if (!hasDoneSubtask)
                    return Json(new
                    {
                        success = false,
                        message = "Complete at least one subtask (mark it Done) before submitting the task."
                    });
            }

            // ─── APPLY ───
            task.Status = newStatus;

            if (newStatus == AppTaskStatus.Done && task.SubmittedAt == null)
            {
                task.SubmittedAt = DateTime.UtcNow;
                task.SubmittedById = userId;
            }
            if (newStatus == AppTaskStatus.Completed)
            {
                task.CompletedAt = DateTime.UtcNow;
                task.CompletedById = userId;
            }
            if (newStatus == AppTaskStatus.ToDo || newStatus == AppTaskStatus.InProgress || newStatus == AppTaskStatus.Planning)
            {
                task.SubmittedAt = null;
                task.SubmittedById = null;
                task.CompletedAt = null;
                task.CompletedById = null;
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await RecomputeProjectProgress(task.ProjectId);

            return Json(new { success = true, message = $"Status changed to {newStatus}." });
        }

        // ============================================
        // POST: /Tasks/DeleteTask
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTask(Guid id)
        {
            var orgId = CurrentOrgId;
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId);
            if (task == null) return Json(new { success = false, message = "Task not found." });

            // Members cannot delete
            if (IsMember) return Json(new { success = false, message = "Members cannot delete tasks." });

            var projectId = task.ProjectId;
            _db.Tasks.Remove(task);
            await _db.SaveChangesAsync();
            await RecomputeProjectProgress(projectId);

            return Json(new { success = true, message = "Task deleted." });
        }

        // ============================================
        // SUBTASKS
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubtask(Guid taskId, string title, string? description)
        {
            var orgId = CurrentOrgId;
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OrganizationId == orgId);
            if (task == null) return Json(new { success = false, message = "Task not found." });

            if (string.IsNullOrWhiteSpace(title))
                return Json(new { success = false, message = "Subtask title is required." });

            var orderIndex = await _db.TaskSubtasks.Where(s => s.TaskId == taskId).CountAsync();

            var sub = new TaskSubtask
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                Title = title.Trim(),
                Description = description?.Trim(),
                Status = SubtaskStatus.Planning,
                OrderIndex = orderIndex
            };
            _db.TaskSubtasks.Add(sub);
            await _db.SaveChangesAsync();

            return Json(new { success = true, id = sub.Id, message = "Subtask added." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSubtaskStatus(Guid id, string status)
        {
            var orgId = CurrentOrgId;
            var sub = await _db.TaskSubtasks.Include(s => s.Task).FirstOrDefaultAsync(s => s.Id == id);
            if (sub?.Task == null || sub.Task.OrganizationId != orgId)
                return Json(new { success = false, message = "Subtask not found." });

            if (!Enum.TryParse<SubtaskStatus>(status, out var st))
                return Json(new { success = false, message = "Invalid status." });

            sub.Status = st;
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Subtask updated." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubtask(Guid id)
        {
            var sub = await _db.TaskSubtasks.Include(s => s.Task).FirstOrDefaultAsync(s => s.Id == id);
            if (sub?.Task == null || sub.Task.OrganizationId != CurrentOrgId)
                return Json(new { success = false, message = "Subtask not found." });

            _db.TaskSubtasks.Remove(sub);
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Subtask removed." });
        }

        // ============================================
        // CHALLENGES
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateChallenge(Guid taskId, string title, string? description)
        {
            var orgId = CurrentOrgId;
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OrganizationId == orgId);
            if (task == null) return Json(new { success = false, message = "Task not found." });

            if (string.IsNullOrWhiteSpace(title))
                return Json(new { success = false, message = "Challenge title is required." });

            var ch = new TaskChallenge
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                RaisedByUserId = CurrentUserId,
                Title = title.Trim(),
                Description = description?.Trim(),
                Status = ChallengeStatus.ToDo
            };
            _db.TaskChallenges.Add(ch);
            await _db.SaveChangesAsync();

            return Json(new { success = true, id = ch.Id, message = "Challenge raised." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateChallengeStatus(Guid id, string status)
        {
            var ch = await _db.TaskChallenges.Include(c => c.Task).FirstOrDefaultAsync(c => c.Id == id);
            if (ch?.Task == null || ch.Task.OrganizationId != CurrentOrgId)
                return Json(new { success = false, message = "Challenge not found." });

            if (!Enum.TryParse<ChallengeStatus>(status, out var st))
                return Json(new { success = false, message = "Invalid status." });

            ch.Status = st;
            ch.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Challenge updated." });
        }

        // ============================================
        // PROJECT STATUS FLOW
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkProjectDone(Guid projectId)
        {
            if (!IsPM) return Json(new { success = false, message = "Only a Project Manager can mark a project done." });

            var userId = CurrentUserId;
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == CurrentOrgId);
            if (project == null) return Json(new { success = false, message = "Project not found." });
            if (project.ProjectManagerId != userId) return Json(new { success = false, message = "Not your project." });

            // All tasks must be Completed
            var allDone = await _db.Tasks
                .Where(t => t.ProjectId == projectId)
                .AllAsync(t => t.Status == AppTaskStatus.Completed);

            if (!allDone) return Json(new { success = false, message = "All tasks must be Completed first." });

            project.Status = ProjectStatus.Done;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Project marked Done. Awaiting Executive Manager confirmation." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmProjectComplete(Guid projectId)
        {
            if (!IsExecutive && !IsAdmin) return Json(new { success = false, message = "Only an Executive Manager can confirm completion." });

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == CurrentOrgId);
            if (project == null) return Json(new { success = false, message = "Project not found." });

            if (project.Status != ProjectStatus.Done)
                return Json(new { success = false, message = "Project must be marked Done by the PM first." });

            project.Status = ProjectStatus.Completed;
            project.Progress = 100;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Project Completed!" });
        }

        // ============================================
        // HELPER: Recompute project progress
        // ============================================
        private async Task RecomputeProjectProgress(Guid projectId)
        {
            var project = await _db.Projects.FindAsync(projectId);
            if (project == null) return;

            var tasks = await _db.Tasks.Where(t => t.ProjectId == projectId).ToListAsync();
            if (tasks.Count == 0)
            {
                project.Progress = 0;
                project.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return;
            }

            var finished = tasks.Count(t =>
                t.Status == AppTaskStatus.Done ||
                t.Status == AppTaskStatus.Completed);

            project.Progress = (int)Math.Round((double)finished / tasks.Count * 100);
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}