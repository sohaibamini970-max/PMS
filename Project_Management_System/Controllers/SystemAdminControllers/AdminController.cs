using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Management_System.Data;
using Project_Management_System.Models;
using Project_Management_System.ViewModels;

namespace Project_Management_System.Controllers
{
    [Authorize(Roles = "SystemAdmin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;

        public AdminController(AppDbContext db)
        {
            _db = db;
        }

        // ============================================
        // HELPERS
        // ============================================
        private Guid CurrentOrgId =>
            Guid.Parse(User.FindFirst("OrganizationId")!.Value);

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private string CurrentOrgName =>
            User.FindFirst("OrganizationName")?.Value ?? "Your Organization";

        // ============================================
        // GET: /Admin/Index
        // ============================================
        public async Task<IActionResult> Index()
        {
            var orgId = CurrentOrgId;

            // Load all users in this org (excluding SuperAdmins)
            var users = await _db.Users
                .Where(u => u.OrganizationId == orgId)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FullName)
                .Select(u => new OrgUserListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role,
                    Status = u.Status,
                    LastLoginAt = u.LastLoginAt,
                    CreatedAt = u.CreatedAt,
                    ProfilePictureUrl = u.ProfilePictureUrl
                })
                .ToListAsync();

            // Stats
            ViewBag.Stats = new AdminStatsViewModel
            {
                TotalUsers = users.Count,
                ExecutiveManagers = users.Count(u => u.Role == UserRole.ExecutiveManager),
                ProjectManagers = users.Count(u => u.Role == UserRole.ProjectManager),
                Members = users.Count(u => u.Role == UserRole.Member),
                SystemAdmins = users.Count(u => u.Role == UserRole.SystemAdmin),
                ActiveUsers = users.Count(u => u.Status == UserStatus.Active),
                InactiveUsers = users.Count(u => u.Status != UserStatus.Active)
            };

            ViewBag.OrgName = CurrentOrgName;

            return View(users);
        }

        // ============================================
        // POST: /Admin/CreateUser
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateOrgUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            // Cannot create SuperAdmin from here
            if (model.Role == UserRole.SuperAdmin)
                return Json(new { success = false, message = "You cannot create Super Admins." });

            var orgId = CurrentOrgId;

            // Check duplicate email in this org
            var emailExists = await _db.Users
                .AnyAsync(u => u.OrganizationId == orgId && u.Email.ToLower() == model.Email.ToLower().Trim());

            if (emailExists)
                return Json(new { success = false, message = "A user with this email already exists in your organization." });

            var user = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                Email = model.Email.ToLower().Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                FullName = model.FullName.Trim(),
                Phone = model.Phone?.Trim(),
                Role = model.Role,
                Status = UserStatus.Active,
                EmailConfirmed = true,
                CreatedByUserId = CurrentUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"{model.Role} '{user.FullName}' created successfully.",
                userId = user.Id
            });
        }

        // ============================================
        // GET: /Admin/GetUser/{id}
        // ============================================
        [HttpGet]
        public async Task<IActionResult> GetUser(Guid id)
        {
            var orgId = CurrentOrgId;

            var user = await _db.Users
                .Where(u => u.Id == id && u.OrganizationId == orgId)
                .Select(u => new
                {
                    id = u.Id,
                    fullName = u.FullName,
                    email = u.Email,
                    phone = u.Phone,
                    role = u.Role.ToString(),
                    status = u.Status.ToString()
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();
            return Json(user);
        }

        // ============================================
        // POST: /Admin/UpdateUser
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(UpdateOrgUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            if (model.Role == UserRole.SuperAdmin)
                return Json(new { success = false, message = "Invalid role." });

            var orgId = CurrentOrgId;

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == model.Id && u.OrganizationId == orgId);

            if (user == null)
                return Json(new { success = false, message = "User not found." });

            // Prevent changing own role away from SystemAdmin
            if (user.Id == CurrentUserId && model.Role != UserRole.SystemAdmin)
                return Json(new { success = false, message = "You cannot change your own role." });

            // Check duplicate email (excluding self)
            var emailExists = await _db.Users
                .AnyAsync(u => u.OrganizationId == orgId
                            && u.Id != model.Id
                            && u.Email.ToLower() == model.Email.ToLower().Trim());

            if (emailExists)
                return Json(new { success = false, message = "Another user already has this email." });

            user.FullName = model.FullName.Trim();
            user.Email = model.Email.ToLower().Trim();
            user.Phone = model.Phone?.Trim();
            user.Role = model.Role;
            user.Status = model.Status;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "User updated successfully." });
        }

        // ============================================
        // POST: /Admin/DeleteUser
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var orgId = CurrentOrgId;

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == orgId);

            if (user == null)
                return Json(new { success = false, message = "User not found." });

            // Cannot delete yourself
            if (user.Id == CurrentUserId)
                return Json(new { success = false, message = "You cannot delete your own account." });

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "User deleted." });
        }

        // ============================================
        // POST: /Admin/ToggleStatus
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var orgId = CurrentOrgId;

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == orgId);

            if (user == null)
                return Json(new { success = false, message = "User not found." });

            if (user.Id == CurrentUserId)
                return Json(new { success = false, message = "You cannot deactivate yourself." });

            user.Status = user.Status == UserStatus.Active
                ? UserStatus.Inactive
                : UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, newStatus = user.Status.ToString() });
        }
    }
}