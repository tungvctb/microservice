using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Apply migration với retry — Postgres thường chưa sẵn sàng khi container service vừa lên.
    /// Hết số lần thử vẫn hỏng thì KHÔNG ném exception: service vẫn khởi động để
    /// /health/live trả lời được, còn /health/ready sẽ báo postgres down.
    /// Crash cả tiến trình ở đây chỉ khiến orchestrator restart vòng lặp mà không cho biết lý do.
    /// </summary>
    /// <returns>true nếu migration đã được apply.</returns>
    public static async Task<bool> MigrateWithRetryAsync<TContext>(
        IServiceProvider services, int maxAttempts = 10, int delaySeconds = 3)
        where TContext : DbContext
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(TContext).Name);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("Đã apply migration cho {Context}", typeof(TContext).Name);
                return true;
            }
            catch (Exception ex)
            {
                if (attempt == maxAttempts)
                {
                    logger.LogError(ex,
                        "Không apply được migration sau {Attempts} lần thử. " +
                        "Service vẫn khởi động nhưng sẽ báo NOT READY cho tới khi có database.",
                        maxAttempts);
                    return false;
                }

                logger.LogWarning("Chưa kết nối được Postgres (lần {Attempt}/{Max}): {Message}",
                    attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }

        return false;
    }
}
