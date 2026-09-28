using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Management_System.Data;
using Project_Management_System.Models;
using System.Security.Claims;
using System.Security.Principal;

namespace Project_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;

        public AccountController(AppDbContext db)
        {
            _db = db;
        }

        // ============================================
        // GET: /Account/Login
        // ============================================
        [HttpGet]
        public IActionResult Login(string? error = null)
        {
            // If already logged in, redirect based on role
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectBasedOnRole(User.FindFirst(ClaimTypes.Role)?.Value);
            }

            if (!string.IsNullOrEmpty(error))
                ViewBag.Error = error;

            return View();
        }

        // ============================================
        // POST: /Account/Login
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            // ============================================
            // DIAGNOSTIC TIMER (temporary — remove later)
            // ============================================
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var timings = new List<string>();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter both email and password.";
                return View();
            }

            var normalizedEmail = email.ToLower().Trim();
            timings.Add($"prep:{sw.ElapsedMilliseconds}ms");

            // ============================================
            // 1. LOOK UP USER
            // ============================================
            sw.Restart();
            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail);
            timings.Add($"db:{sw.ElapsedMilliseconds}ms");

            if (user == null)
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            // ============================================
            // 2. VERIFY PASSWORD
            // ============================================
            sw.Restart();
            var passwordOk = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            timings.Add($"bcrypt:{sw.ElapsedMilliseconds}ms");

            if (!passwordOk)
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            // ============================================
            // 3. STATUS CHECKS
            // ============================================
            if (user.Status != UserStatus.Active)
            {
                ViewBag.Error = $"Your account is {user.Status.ToString().ToLower()}. Please contact your administrator.";
                return View();
            }

            if (user.Role != UserRole.SuperAdmin && user.Organization != null)
            {
                if (user.Organization.Status == "Suspended" || user.Organization.Status == "Expired")
                {
                    ViewBag.Error = "Your organization's access has been suspended. Please contact support.";
                    return View();
                }
            }

            // ============================================
            // 4. BUILD CLAIMS  ✅ THESE LINES ARE REQUIRED
            // ============================================
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.FullName),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("FullName", user.FullName),
        new Claim("Role", user.Role.ToString()),
        new Claim("OrganizationId", user.OrganizationId?.ToString() ?? ""),
        new Claim("OrganizationName", user.Organization?.Name ?? "Platform"),
        new Claim("ProfilePicture", user.ProfilePictureUrl ?? "")
    };

            // ✅ THESE TWO LINES MUST BE HERE BEFORE SignInAsync
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // ============================================
            // 5. SIGN IN
            // ============================================
            sw.Restart();
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,   // ← now this exists ✅
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTime.UtcNow.AddHours(8)
                });
            timings.Add($"signin:{sw.ElapsedMilliseconds}ms");

            // ============================================
            // 6. UPDATE LAST LOGIN (fire-and-forget)
            // ============================================
            var userId = user.Id;
            var now = DateTime.UtcNow;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.ExecuteSqlRawAsync(
                        "UPDATE users SET last_login_at = {0} WHERE id = {1}",
                        now, userId);
                }
                catch { /* silent */ }
            });

            // ============================================
            // 7. LOG TIMINGS
            // ============================================
            Console.WriteLine($"[LOGIN] {normalizedEmail} → {string.Join(" | ", timings)} | TOTAL: {sw.ElapsedMilliseconds}ms");

            return RedirectBasedOnRole(user.Role.ToString());
        }

        // ============================================
        // GET: /Account/Logout
        // ============================================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        // ============================================
        // GET: /Account/AccessDenied
        // ============================================
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ============================================
        // HELPER: Route by role
        // ============================================
        private IActionResult RedirectBasedOnRole(string? role)
        {
            return role switch
            {
                "SuperAdmin" => RedirectToAction("Index", "SuperAdmin"),
                "SystemAdmin" => RedirectToAction("Index", "Admin"),
                _ => RedirectToAction("Index", "Home")
            };
        }
    }
}