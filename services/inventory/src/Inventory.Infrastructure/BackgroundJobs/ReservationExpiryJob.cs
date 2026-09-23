using BuildingBlocks.Application.Cqrs;
using Inventory.Application.Reservations.Commands;
using Inventory.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.BackgroundJobs;

/// <summary>
/// Nhả các giữ chỗ quá hạn. Không có job này, một đơn hàng bị bỏ dở giữa chừng
/// sẽ khóa hàng vĩnh viễn và kho "còn" nhưng không ai mua được.
/// </summary>
public sealed class ReservationExpiryJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpiryJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Job dọn giữ chỗ hết hạn khởi động (chu kỳ {Interval})", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reservations = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

                var expired = await reservations.GetExpiredAsync(50, stoppingToken);

                foreach (var reservation in expired)
                {
                    var result = await dispatcher.Send(
                        new ReleaseReservationCommand(reservation.OrderId, "Hết hạn giữ chỗ"), stoppingToken);

                    if (result.IsSuccess)
                        logger.LogInformation("Đã nhả giữ chỗ hết hạn của đơn {OrderId}", reservation.OrderId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job dọn giữ chỗ gặp lỗi");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
