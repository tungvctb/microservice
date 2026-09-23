using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Product.Domain.Entities;

namespace Product.Infrastructure.Persistence;

public static class ProductDbInitializer
{
    /// <summary>Apply migration và seed dữ liệu mẫu — chỉ dùng cho môi trường dev/demo.</summary>
    public static async Task InitializeAsync(IServiceProvider services, bool seed)
    {
        var migrated = await DatabaseInitializer.MigrateWithRetryAsync<ProductDbContext>(services);
        if (!migrated || !seed) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ProductDbInitializer");

        if (await db.Categories.AnyAsync()) return;

        var electronics = Category.Create("Điện tử", "Thiết bị điện tử, phụ kiện công nghệ");
        var fashion = Category.Create("Thời trang", "Quần áo, giày dép, phụ kiện");
        var home = Category.Create("Gia dụng", "Đồ dùng nhà bếp và sinh hoạt");
        db.Categories.AddRange(electronics, fashion, home);

        db.Products.AddRange(
            ProductItem.Create("LAP-001", "Laptop Dell XPS 13", "Ultrabook 13 inch, Core i7, 16GB RAM",
                32_990_000m, "VND", electronics.Id, "https://picsum.photos/seed/lap001/600/400", 25),
            ProductItem.Create("PHN-002", "iPhone 15 Pro", "256GB, Titan tự nhiên",
                28_490_000m, "VND", electronics.Id, "https://picsum.photos/seed/phn002/600/400", 40),
            ProductItem.Create("HDP-003", "Tai nghe Sony WH-1000XM5", "Chống ồn chủ động",
                7_990_000m, "VND", electronics.Id, "https://picsum.photos/seed/hdp003/600/400", 60),
            ProductItem.Create("SHT-004", "Áo sơ mi linen", "Chất liệu linen mát, form regular",
                450_000m, "VND", fashion.Id, "https://picsum.photos/seed/sht004/600/400", 120),
            ProductItem.Create("SHO-005", "Giày sneaker trắng", "Da tổng hợp, đế cao su",
                890_000m, "VND", fashion.Id, "https://picsum.photos/seed/sho005/600/400", 80),
            ProductItem.Create("POT-006", "Nồi chiên không dầu 5L", "Công suất 1500W, 8 chế độ",
                1_690_000m, "VND", home.Id, "https://picsum.photos/seed/pot006/600/400", 35),
            ProductItem.Create("BLD-007", "Máy xay sinh tố", "Cối thủy tinh 1.5L",
                790_000m, "VND", home.Id, "https://picsum.photos/seed/bld007/600/400", 50));

        await db.SaveChangesAsync();
        logger.LogInformation("Đã seed {Categories} danh mục và {Products} sản phẩm mẫu", 3, 7);
    }
}
