using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Project_Management_System.Data;
using Project_Management_System.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. DATABASE — Neon PostgreSQL
// ============================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("NeonDb"),
        npgsqlOptions =>
        {
            // Retry transient network failures automatically
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);

            npgsqlOptions.CommandTimeout(60);
        }));

// ============================================
// 2. BACKGROUND KEEP-ALIVE
// Pings the DB every 4 min so Neon doesn't
// suspend → first login stays fast
// ============================================
builder.Services.AddHostedService<DbKeepAliveService>();

// ============================================
// 3. AUTHENTICATION — Cookie-based
// ============================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "ARG_PMS_Auth";
    });

builder.Services.AddAuthorization();

// ============================================
// 4. MVC
// ============================================
builder.Services.AddControllersWithViews();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// ============================================
// 5. STARTUP: WARM-UP + SEED
// ============================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Warm up the connection so the first user doesn't wait
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        Console.WriteLine(">>> Database warmed up and ready");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> Database warm-up warning: {ex.Message}");
    }

    // Seed the super admin (idempotent)
    await DbSeeder.SeedSuperAdminAsync(db);
}

// ============================================
// 6. PIPELINE
// ============================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();   // Must come before UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
