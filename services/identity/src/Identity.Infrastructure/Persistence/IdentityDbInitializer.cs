using BuildingBlocks.Infrastructure.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Persistence;

public static class IdentityDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool seed)
    {
        var migrated = await DatabaseInitializer.MigrateWithRetryAsync<IdentityDbContext>(services);
        if (!migrated || !seed) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDbInitializer");

        if (await db.Users.AnyAsync()) return;

        db.Users.AddRange(
            User.Create("admin@shop.local", hasher.Hash("Admin@123"), "Quản trị hệ thống",
                "0900000001", UserRoles.Admin, UserRoles.Manager),
            User.Create("manager@shop.local", hasher.Hash("Manager@123"), "Nhân viên vận hành",
                "0900000002", UserRoles.Manager),
            User.Create("customer@shop.local", hasher.Hash("Customer@123"), "Khách hàng demo",
                "0900000003", UserRoles.Customer));

        await db.SaveChangesAsync();
        logger.LogInformation("Đã tạo 3 tài khoản mẫu (admin/manager/customer @shop.local)");
    }
}
