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
    public class TeamsController : Controller
    {
        private readonly AppDbContext _db;
        public TeamsController(AppDbContext db) => _db = db;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private Guid CurrentOrgId => Guid.Parse(User.FindFirst("OrganizationId")!.Value);
        private string CurrentRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        private bool IsExec => CurrentRole == "ExecutiveManager";
        private bool IsAdmin => CurrentRole == "SystemAdmin";
        private bool IsPM => CurrentRole == "ProjectManager";
        private bool IsMember => CurrentRole == "Member";

        // ============================================
        // GET: /Teams
        // ============================================
        public async Task<IActionResult> Index()
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            // ── Determine if current user leads any team ──
            var leadingTeamIds = await _db.Teams
                .Where(t => t.OrganizationId == orgId && t.LeaderId == userId)
                .Select(t => t.Id)
                .ToListAsync();

            var isTeamLeader = leadingTeamIds.Any();

            // ── Member IDs of the teams the user leads ──
            var leaderTeamMemberIds = isTeamLeader
                ? await _db.TeamMembers
                    .Where(tm => leadingTeamIds.Contains(tm.TeamId))
                    .Select(tm => tm.UserId)
                    .Distinct()
                    .ToListAsync()
                : new List<Guid>();

            // ── Project query (role-aware) ──
            IQueryable<Project> projectQuery = _db.Projects
                .Where(p => p.OrganizationId == orgId);

            if (IsPM)
            {
                projectQuery = projectQuery.Where(p => p.ProjectManagerId == userId);
            }
            else if (isTeamLeader && !IsExec && !IsAdmin)
            {
                projectQuery = projectQuery.Where(p =>
                    (p.AssignedTeamId.HasValue && leadingTeamIds.Contains(p.AssignedTeamId.Value)) ||
                    (p.AssignedMemberId.HasValue && leaderTeamMemberIds.Contains(p.AssignedMemberId.Value)) ||
                    _db.Tasks.Any(t => t.ProjectId == p.Id &&
                        ((t.AssigneeTeamId.HasValue && leadingTeamIds.Contains(t.AssigneeTeamId.Value)) ||
                         (t.AssigneeId.HasValue && leaderTeamMemberIds.Contains(t.AssigneeId.Value)) ||
                         t.AssigneeId == userId))
                );
            }

            var projectRows = await projectQuery
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Client,
                    p.Status,
                    p.Progress,
                    p.AssignedTeamId,
                    p.AssignedMemberId,
                    AssignedTeamName = p.AssignedTeam != null ? p.AssignedTeam.Name : null,
                    AssignedMemberName = p.AssignedMember != null ? p.AssignedMember.FullName : null
                })
                .ToListAsync();

            var projectIds = projectRows.Select(p => p.Id).ToList();

            // ── Task query (role-aware) ──
            // ── Task query (role-aware) ──
            // PM / Exec / Admin → all tasks of visible projects
            // Team leader       → all tasks of visible projects (so unassigned tasks are visible to assign)
            // Member (non-leader) → only their own tasks
            IQueryable<TaskItem> taskQuery = _db.Tasks
                .Where(t => projectIds.Contains(t.ProjectId));

            if (IsMember && !isTeamLeader)
            {
                taskQuery = taskQuery.Where(t => t.AssigneeId == userId);
            }

            var taskRows = await taskQuery
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.ProjectId,
                    t.Title,
                    t.Description,
                    t.Priority,
                    t.Status,
                    t.Deadline,
                    t.AssigneeId,
                    AssigneeName = t.Assignee != null ? t.Assignee.FullName : null,
                    AssigneePicture = t.Assignee != null ? t.Assignee.ProfilePictureUrl : null,
                    t.AssigneeTeamId,
                    AssigneeTeamName = t.AssigneeTeam != null ? t.AssigneeTeam.Name : null,
                    SubtaskCount = t.Subtasks.Count,
                    SubtaskDone = t.Subtasks.Count(s => s.Status == SubtaskStatus.Done),
                    ChallengeCount = t.Challenges.Count
                })
                .ToListAsync();

            var tasksByProject = taskRows
                .GroupBy(t => t.ProjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var projects = projectRows.Select(p => new ProjectWithTasksViewModel
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
                AssignedTeamName = p.AssignedTeamName,
                AssignedMemberId = p.AssignedMemberId,
                AssignedMemberName = p.AssignedMemberName,
                Tasks = tasksByProject.TryGetValue(p.Id, out var ts)
                    ? ts.Select(t => new TaskListItemViewModel
                    {
                        Id = t.Id,
                        ProjectId = t.ProjectId,
                        Title = t.Title,
                        Description = t.Description,
                        Priority = t.Priority,
                        Status = t.Status,
                        Deadline = t.Deadline,
                        AssigneeId = t.AssigneeId,
                        AssigneeName = t.AssigneeName,
                        AssigneePicture = t.AssigneePicture,
                        AssigneeTeamId = t.AssigneeTeamId,
                        AssigneeTeamName = t.AssigneeTeamName,
                        SubtaskCount = t.SubtaskCount,
                        SubtaskDone = t.SubtaskDone,
                        ChallengeCount = t.ChallengeCount
                    }).ToList()
                    : new List<TaskListItemViewModel>()
            }).ToList();

            // ── Teams ── (member role: only teams they belong to; leader: only teams they lead OR belong to)
            var teamsQuery = _db.Teams.Where(t => t.OrganizationId == orgId);

            if (IsMember)
                teamsQuery = teamsQuery.Where(t => t.Members.Any(m => m.UserId == userId));
            else if (isTeamLeader && !IsExec && !IsAdmin && !IsPM)
                teamsQuery = teamsQuery.Where(t =>
                    t.LeaderId == userId ||
                    t.Members.Any(m => m.UserId == userId));

            // 1. Team base data (no counts)
            var teamRows = await teamsQuery
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Description,
                    t.Color,
                    t.LeaderId,
                    LeaderName = t.Leader != null ? t.Leader.FullName : null,
                    LeaderPicture = t.Leader != null ? t.Leader.ProfilePictureUrl : null,
                    Members = t.Members.Select(m => new TeamMemberViewModel
                    {
                        UserId = m.UserId,
                        FullName = m.User.FullName,
                        ProfilePictureUrl = m.User.ProfilePictureUrl,
                        Role = m.User.Role.ToString(),
                        IsLeader = m.UserId == t.LeaderId
                    }).ToList()
                })
                .ToListAsync();

            var teamIds = teamRows.Select(t => t.Id).ToList();

            // 2. Project counts per team (separate query)
            var teamProjectCounts = teamIds.Count == 0
                ? new Dictionary<Guid, int>()
                : await _db.Projects
                    .Where(p => p.AssignedTeamId != null && teamIds.Contains(p.AssignedTeamId.Value))
                    .GroupBy(p => p.AssignedTeamId!.Value)
                    .Select(g => new { TeamId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.TeamId, x => x.Count);

            // 3. Task counts per team (separate query)
            // 3. Task counts per team — includes BOTH team-level AND member-level assignments
            var teamTaskCounts = new Dictionary<Guid, int>();

            if (teamIds.Count > 0)
            {
                // (a) Tasks assigned to the team as a whole
                var teamLevelCounts = await _db.Tasks
                    .Where(t => t.AssigneeTeamId != null && teamIds.Contains(t.AssigneeTeamId.Value))
                    .GroupBy(t => t.AssigneeTeamId!.Value)
                    .Select(g => new { TeamId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.TeamId, x => x.Count);

                foreach (var kv in teamLevelCounts)
                    teamTaskCounts[kv.Key] = kv.Value;

                // (b) Tasks assigned to individual members of each team
                var teamMembers = await _db.TeamMembers
                    .Where(tm => teamIds.Contains(tm.TeamId))
                    .Select(tm => new { tm.TeamId, tm.UserId })
                    .ToListAsync();

                var memberIds = teamMembers.Select(tm => tm.UserId).Distinct().ToList();

                var memberTaskCounts = memberIds.Count == 0
                    ? new Dictionary<Guid, int>()
                    : await _db.Tasks
                        .Where(t => t.AssigneeId != null && memberIds.Contains(t.AssigneeId.Value))
                        .GroupBy(t => t.AssigneeId!.Value)
                        .Select(g => new { UserId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.UserId, x => x.Count);

                // Attribute each member's task count to every team they belong to
                foreach (var tm in teamMembers)
                {
                    if (memberTaskCounts.TryGetValue(tm.UserId, out var cnt))
                    {
                        teamTaskCounts.TryGetValue(tm.TeamId, out var existing);
                        teamTaskCounts[tm.TeamId] = existing + cnt;
                    }
                }
            }

            // 4. Stitch them together
            var teams = teamRows.Select(t => new TeamViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Color = t.Color,
                LeaderId = t.LeaderId,
                LeaderName = t.LeaderName,
                LeaderPicture = t.LeaderPicture,
                Members = t.Members,
                ProjectCount = teamProjectCounts.GetValueOrDefault(t.Id, 0),
                TaskCount = teamTaskCounts.GetValueOrDefault(t.Id, 0)
            }).ToList();
            // ── Members ──
            var membersQuery = _db.Users
    .Where(u => u.OrganizationId == orgId
             && u.Status == UserStatus.Active
             && u.Role == UserRole.Member);

            if (IsMember && !isTeamLeader)
            {
                // Plain member: only themselves
                membersQuery = membersQuery.Where(u => u.Id == userId);
            }
            else if (IsMember && isTeamLeader)
            {
                // Team leader: themselves + their team members
                membersQuery = membersQuery.Where(u =>
                    u.Id == userId || leaderTeamMemberIds.Contains(u.Id));
            }

            var members = await membersQuery
                .OrderBy(u => u.FullName)
                .Select(u => new MemberViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    Role = u.Role.ToString(),
                    ProjectCount = _db.Projects.Count(p => p.AssignedMemberId == u.Id),
                    TaskCount = _db.Tasks.Count(t => t.AssigneeId == u.Id)
                })
                .ToListAsync();

            var vm = new TeamsIndexViewModel
            {
                Projects = projects,
                Teams = teams,
                Members = members,
                CanManage = IsExec || IsAdmin || IsPM || isTeamLeader,
                IsMemberRole = IsMember,
                IsTeamLeader = isTeamLeader,
                LeadingTeamIds = leadingTeamIds,
                CurrentUserId = userId
            };

            return View(vm);
        }

        // ============================================
        // POST: /Teams/CreateTeam
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeam(string name, string? description, string color)
        {
            if (!IsExec && !IsAdmin && !IsPM)
                return Json(new { success = false, message = "You don't have permission to create teams." });
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "Team name is required." });

            var team = new Team
            {
                Id = Guid.NewGuid(),
                OrganizationId = CurrentOrgId,
                Name = name.Trim(),
                Description = description?.Trim(),
                Color = string.IsNullOrWhiteSpace(color) ? "emerald" : color,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"Team '{team.Name}' created.", teamId = team.Id });
        }

        // ============================================
        // POST: /Teams/DeleteTeam
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeam(Guid id)
        {
            if (!IsExec && !IsAdmin && !IsPM)
                return Json(new { success = false, message = "You don't have permission to delete teams." });

            var team = await _db.Teams
                .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == CurrentOrgId);
            if (team == null) return Json(new { success = false, message = "Team not found." });

            _db.Teams.Remove(team);
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Team deleted." });
        }

        // ============================================
        // POST: /Teams/AddMembers   (bulk)
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMembers(Guid teamId, Guid[] userIds)
        {
            if (!IsExec && !IsAdmin && !IsPM)
                return Json(new { success = false, message = "You don't have permission." });

            if (userIds == null || userIds.Length == 0)
                return Json(new { success = false, message = "Select at least one member." });

            var team = await _db.Teams
                .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == CurrentOrgId);
            if (team == null) return Json(new { success = false, message = "Team not found." });

            // Only add users from this org who aren't already in
            var validIds = await _db.Users
                .Where(u => userIds.Contains(u.Id)
                         && u.OrganizationId == CurrentOrgId
                         && u.Status == UserStatus.Active)
                .Select(u => u.Id)
                .ToListAsync();

            var existing = await _db.TeamMembers
                .Where(tm => tm.TeamId == teamId && validIds.Contains(tm.UserId))
                .Select(tm => tm.UserId)
                .ToListAsync();

            var toAdd = validIds.Except(existing).Select(uid => new TeamMember
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                UserId = uid,
                AddedAt = DateTime.UtcNow
            }).ToList();

            _db.TeamMembers.AddRange(toAdd);
            team.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"{toAdd.Count} member(s) added." });
        }

        // ============================================
        // POST: /Teams/RemoveMember
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(Guid teamId, Guid userId)
        {
            if (!IsExec && !IsAdmin && !IsPM)
                return Json(new { success = false, message = "You don't have permission." });

            var tm = await _db.TeamMembers
                .FirstOrDefaultAsync(x => x.TeamId == teamId && x.UserId == userId);
            if (tm == null) return Json(new { success = false, message = "Member not in team." });

            _db.TeamMembers.Remove(tm);
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Member removed from team." });
        }

        // ============================================
        // POST: /Teams/AssignProject
        // targetType: "team" | "member" | "none"
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignProject(Guid projectId, string targetType, Guid targetId)
        {
            if (!IsExec && !IsAdmin && !IsPM)
                return Json(new { success = false, message = "You don't have permission." });

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId && p.OrganizationId == CurrentOrgId);
            if (project == null) return Json(new { success = false, message = "Project not found." });

            if (IsPM && project.ProjectManagerId != CurrentUserId)
                return Json(new { success = false, message = "Only your own projects can be assigned." });

            // Clear both, then set only the one requested
            project.AssignedTeamId = null;
            project.AssignedMemberId = null;

            if (targetType == "team")
            {
                var team = await _db.Teams
                    .FirstOrDefaultAsync(t => t.Id == targetId && t.OrganizationId == CurrentOrgId);
                if (team == null) return Json(new { success = false, message = "Team not found." });
                project.AssignedTeamId = team.Id;
            }
            else if (targetType == "member")
            {
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.Id == targetId && u.OrganizationId == CurrentOrgId);
                if (user == null) return Json(new { success = false, message = "Member not found." });
                project.AssignedMemberId = user.Id;
            }
            else if (targetType != "none")
            {
                return Json(new { success = false, message = "Invalid target type." });
            }

            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Project assignment updated." });
        }

        // ============================================
        // POST: /Teams/AssignTask
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTask(Guid taskId, string targetType, Guid targetId)
        {
            var orgId = CurrentOrgId;
            var userId = CurrentUserId;

            var task = await _db.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.OrganizationId == orgId);
            if (task == null) return Json(new { success = false, message = "Task not found." });

            // Determine current user's permission
            var isExecOrAdmin = IsExec || IsAdmin;
            var isOwnerPm = IsPM && task.Project?.ProjectManagerId == userId;

            // Team leader check
            var leadingTeamIds = await _db.Teams
                .Where(t => t.OrganizationId == orgId && t.LeaderId == userId)
                .Select(t => t.Id)
                .ToListAsync();
            var isTeamLeader = leadingTeamIds.Any();

            if (!isExecOrAdmin && !isOwnerPm && !isTeamLeader)
                return Json(new { success = false, message = "You don't have permission to assign this task." });

            // Leaders can only assign to members of their own teams
            if (isTeamLeader && !isExecOrAdmin && !isOwnerPm && targetType == "member")
            {
                var targetInLeaderTeam = await _db.TeamMembers
                    .AnyAsync(tm => leadingTeamIds.Contains(tm.TeamId) && tm.UserId == targetId);
                if (!targetInLeaderTeam && targetId != userId)
                    return Json(new { success = false, message = "You can only assign tasks to members of your team." });
            }

            // Leaders can only assign to their own team
            if (isTeamLeader && !isExecOrAdmin && !isOwnerPm && targetType == "team")
            {
                if (!leadingTeamIds.Contains(targetId))
                    return Json(new { success = false, message = "You can only assign tasks to your own team." });
            }

            task.AssigneeId = null;
            task.AssigneeTeamId = null;

            if (targetType == "team")
            {
                var team = await _db.Teams
                    .FirstOrDefaultAsync(t => t.Id == targetId && t.OrganizationId == orgId);
                if (team == null) return Json(new { success = false, message = "Team not found." });
                task.AssigneeTeamId = team.Id;
            }
            else if (targetType == "member")
            {
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.Id == targetId && u.OrganizationId == orgId);
                if (user == null) return Json(new { success = false, message = "Member not found." });
                task.AssigneeId = user.Id;
            }
            else if (targetType != "none")
            {
                return Json(new { success = false, message = "Invalid target type." });
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Task assignment updated." });
        }

        // ============================================
        // POST: /Teams/SetLeader
        // Assign a team leader (only for Member-role users in the team)
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLeader(Guid teamId, Guid userId)
        {
            if (!IsExec && !IsAdmin && !IsPM)
                return Json(new { success = false, message = "Only Executive Managers, System Admins and Project Managers can set a team leader." });

            var team = await _db.Teams
                .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == CurrentOrgId);
            if (team == null) return Json(new { success = false, message = "Team not found." });

            // Leader must be a Member-role user who is part of this team
            var userIsMemberOfTeam = await _db.TeamMembers
                .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId);
            if (!userIsMemberOfTeam)
                return Json(new { success = false, message = "That user is not in this team." });

            var isMemberRole = await _db.Users
                .AnyAsync(u => u.Id == userId
                            && u.OrganizationId == CurrentOrgId
                            && u.Role == UserRole.Member
                            && u.Status == UserStatus.Active);
            if (!isMemberRole)
                return Json(new { success = false, message = "Team leader must be a Member role user." });

            team.LeaderId = userId;
            team.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Team leader assigned." });
        }

        // ============================================
        // POST: /Teams/RemoveLeader
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveLeader(Guid teamId)
        {
            if (!IsExec && !IsAdmin && !IsPM)
                return Json(new { success = false, message = "You don't have permission." });

            var team = await _db.Teams
                .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == CurrentOrgId);
            if (team == null) return Json(new { success = false, message = "Team not found." });

            team.LeaderId = null;
            team.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Team leader removed." });
        }
    }
}