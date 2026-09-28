using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Management_System.Data;
using Project_Management_System.Models;
using Project_Management_System.ViewModels;

namespace Project_Management_System.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminController : Controller
    {
        private readonly AppDbContext _db;

        public SuperAdminController(AppDbContext db)
        {
            _db = db;
        }

        // ============================================
        // GET: /SuperAdmin
        // ============================================
        public async Task<IActionResult> Index()
        {
            var orgs = await _db.Organizations
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrganizationListItemViewModel
                {
                    Id = o.Id,
                    Name = o.Name,
                    Slug = o.Slug,
                    Status = o.Status,
                    ExpiresAt = o.ExpiresAt,
                    CreatedAt = o.CreatedAt,
                    AdminName = _db.Users
                        .Where(u => u.OrganizationId == o.Id && u.Role == UserRole.SystemAdmin)
                        .OrderBy(u => u.CreatedAt)
                        .Select(u => u.FullName)
                        .FirstOrDefault(),
                    AdminEmail = _db.Users
                        .Where(u => u.OrganizationId == o.Id && u.Role == UserRole.SystemAdmin)
                        .OrderBy(u => u.CreatedAt)
                        .Select(u => u.Email)
                        .FirstOrDefault(),
                    AdminId = _db.Users
                        .Where(u => u.OrganizationId == o.Id && u.Role == UserRole.SystemAdmin)
                        .OrderBy(u => u.CreatedAt)
                        .Select(u => (Guid?)u.Id)
                        .FirstOrDefault(),
                    TotalUsers = _db.Users.Count(u => u.OrganizationId == o.Id),
                })
                .ToListAsync();

            return View(orgs);
        }

        // ============================================
        // POST: /SuperAdmin/CreateOrganization
        // ============================================
        // ============================================
        // POST: /SuperAdmin/CreateOrganization
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrganization(CreateOrganizationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            // Check slug uniqueness
            var slugExists = await _db.Organizations.AnyAsync(o => o.Slug == model.Slug.ToLower());
            if (slugExists)
                return Json(new { success = false, message = "That subdomain is already taken." });

            // Also check admin email is not already in use
            var emailExists = await _db.Users.AnyAsync(u => u.Email == model.AdminEmail.ToLower());
            if (emailExists)
                return Json(new { success = false, message = "An account with this email already exists." });

            var superAdminId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            // ⬇️ Wrap in the execution strategy so retries are allowed
            var strategy = _db.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _db.Database.BeginTransactionAsync();
                    try
                    {
                        // 1. Create organization
                        var org = new Organization
                        {
                            Id = Guid.NewGuid(),
                            Name = model.OrganizationName.Trim(),
                            Slug = model.Slug.ToLower().Trim(),
                            ContactEmail = model.ContactEmail?.Trim(),
                            Phone = model.Phone?.Trim(),
                            Status = model.Plan.Contains("Trial") ? "Trial" : "Active",
                            ExpiresAt = model.Plan.Contains("Trial")
                                ? DateTime.UtcNow.AddDays(14)
                                : (DateTime?)null,
                            CreatedByUserId = superAdminId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _db.Organizations.Add(org);
                        await _db.SaveChangesAsync();

                        // 2. Create first System Admin for the org
                        var admin = new User
                        {
                            Id = Guid.NewGuid(),
                            OrganizationId = org.Id,
                            Email = model.AdminEmail.ToLower().Trim(),
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.AdminPassword),
                            FullName = model.AdminFullName.Trim(),
                            Role = UserRole.SystemAdmin,
                            Status = UserStatus.Active,
                            EmailConfirmed = true,
                            CreatedByUserId = superAdminId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _db.Users.Add(admin);
                        await _db.SaveChangesAsync();

                        await transaction.CommitAsync();

                        return Json(new
                        {
                            success = true,
                            message = $"Organization '{org.Name}' created successfully!",
                            organizationId = org.Id,
                            adminEmail = admin.Email
                        });
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;   // rethrow so the strategy can retry or fail
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ============================================
        // POST: /SuperAdmin/ToggleStatus
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var org = await _db.Organizations.FindAsync(id);
            if (org == null) return Json(new { success = false, message = "Organization not found." });

            org.Status = org.Status == "Suspended" ? "Active" : "Suspended";
            org.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, newStatus = org.Status });
        }

        // ============================================
        // POST: /SuperAdmin/DeleteOrganization
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrganization(Guid id)
        {
            var org = await _db.Organizations.FindAsync(id);
            if (org == null) return Json(new { success = false, message = "Organization not found." });

            _db.Organizations.Remove(org);   // cascade deletes users
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Organization deleted." });
        }

        // ============================================
        // GET: /SuperAdmin/GetOrganization/{id}
        // ============================================
        [HttpGet]
        public async Task<IActionResult> GetOrganization(Guid id)
        {
            var org = await _db.Organizations
                .Include(o => o.Users)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (org == null) return NotFound();

            var admin = org.Users
                .Where(u => u.Role == UserRole.SystemAdmin)
                .OrderBy(u => u.CreatedAt)
                .FirstOrDefault();

            return Json(new
            {
                id = org.Id,
                name = org.Name,
                slug = org.Slug,
                status = org.Status,
                plan = org.Status == "Trial" ? "Free Trial" : "Pro Plan",
                adminName = admin?.FullName ?? "—",
                adminEmail = admin?.Email ?? "—",
                totalUsers = org.Users.Count,
                createdAt = org.CreatedAt.ToString("MMM dd, yyyy")
            });
        }
    }
}