using Microsoft.EntityFrameworkCore;
using Project_Management_System.Models;

namespace Project_Management_System.Data
{
    public static class DbSeeder
    {
        public static async Task SeedSuperAdminAsync(AppDbContext db)
        {
            // Check if any Super Admin exists
            var exists = await db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin);
            if (exists) return;

            // 🔐 CHANGE THESE BEFORE FIRST RUN
            var email = "superadmin@argpms.com";
            var password = "SuperAdmin@123";   // ← change this!
            var fullName = "Platform Owner";

            var superAdmin = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = null,                       // SuperAdmin has NO organization
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FullName = fullName,
                Role = UserRole.SuperAdmin,
                Status = UserStatus.Active,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.Add(superAdmin);
            await db.SaveChangesAsync();

            Console.WriteLine($"✅ Super Admin seeded: {email}");
        }
    }
}