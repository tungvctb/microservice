using BuildingBlocks.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence;

public static class InventoryDbInitializer
{
    /// <summary>Tồn kho không seed tay: nó sinh ra từ event ProductCreated của catalog.</summary>
    public static Task InitializeAsync(IServiceProvider services) =>
        DatabaseInitializer.MigrateWithRetryAsync<InventoryDbContext>(services);
}
