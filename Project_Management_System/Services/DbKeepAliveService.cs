using Microsoft.EntityFrameworkCore;
using Project_Management_System.Data;

namespace Project_Management_System.Services
{
    /// <summary>
    /// Pings the Neon database every 4 minutes so it doesn't suspend.
    /// Neon free tier suspends after ~5 minutes of inactivity, which
    /// causes the first login after idle periods to take 3-5 seconds.
    /// </summary>
    public class DbKeepAliveService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DbKeepAliveService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(4);

        public DbKeepAliveService(IServiceScopeFactory scopeFactory, ILogger<DbKeepAliveService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[KeepAlive] Started — pinging DB every {Minutes} minutes", Interval.TotalMinutes);

            // Ping immediately on startup
            await PingAsync();

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PingAsync();
            }
        }

        private async Task PingAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.ExecuteSqlRawAsync("SELECT 1");
                _logger.LogDebug("[KeepAlive] DB ping OK at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[KeepAlive] DB ping failed (will retry)");
            }
        }
    }
}