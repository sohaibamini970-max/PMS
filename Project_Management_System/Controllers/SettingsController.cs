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
    public class SettingsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        private const long MaxAvatarBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedImageExtensions =
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public SettingsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // ============================================
        // GET: /Settings
        // ============================================
        public async Task<IActionResult> Index()
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return NotFound();

            var vm = new SettingsViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.ToString(),
                ProfilePictureUrl = user.ProfilePictureUrl,
                Phone = user.Phone ?? ""
            };

            return View(vm);
        }

        // ============================================
        // POST: /Settings/UploadAvatar
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxAvatarBytes)]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select an image file." });

            if (file.Length > MaxAvatarBytes)
                return Json(new { success = false, message = "Image must be under 5 MB." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext))
                return Json(new { success = false, message = "Only JPG, PNG, GIF, or WEBP images are allowed." });

            if (!string.IsNullOrEmpty(file.ContentType) &&
                !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Invalid file type." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            // Remove old local file if present
            DeleteOldLocalAvatar(user.ProfilePictureUrl);

            var relativeDir = "uploads/avatars";
            var absoluteDir = Path.Combine(_env.WebRootPath, relativeDir);
            Directory.CreateDirectory(absoluteDir);

            var storedName = $"{user.Id}_{Guid.NewGuid():N}{ext}";
            var absolutePath = Path.Combine(absoluteDir, storedName);

            using (var stream = System.IO.File.Create(absolutePath))
            {
                await file.CopyToAsync(stream);
            }

            user.ProfilePictureUrl = $"/{relativeDir}/{storedName}";
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Profile picture uploaded.",
                url = user.ProfilePictureUrl
            });
        }

        // ============================================
        // POST: /Settings/SavePreset
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePreset(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return Json(new { success = false, message = "No preset selected." });

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
                return Json(new { success = false, message = "Invalid preset URL." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            // Remove old local file if switching from an upload
            DeleteOldLocalAvatar(user.ProfilePictureUrl);

            user.ProfilePictureUrl = url;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Preset applied.", url });
        }

        // ============================================
        // POST: /Settings/RemoveAvatar
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAvatar()
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            DeleteOldLocalAvatar(user.ProfilePictureUrl);

            user.ProfilePictureUrl = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Profile picture removed." });
        }

        // ============================================
        // POST: /Settings/ChangePassword
        // ============================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
                return Json(new { success = false, message = "Password must be at least 8 characters." });

            if (newPassword != confirmPassword)
                return Json(new { success = false, message = "Passwords do not match." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            // ⚠ MUST match the hasher used by your login action
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Password updated successfully." });
        }

        // ============================================
        // Helper
        // ============================================
        private void DeleteOldLocalAvatar(string? oldUrl)
        {
            if (string.IsNullOrEmpty(oldUrl)) return;
            if (!oldUrl.StartsWith("/uploads/")) return;

            try
            {
                var path = Path.Combine(_env.WebRootPath, oldUrl.TrimStart('/'));
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch { /* best effort */ }
        }
    }
}