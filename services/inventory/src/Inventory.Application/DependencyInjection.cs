using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services) =>
        services.AddCqrs(typeof(DependencyInjection).Assembly);
}
