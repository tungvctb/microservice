using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Product.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProductApplication(this IServiceCollection services) =>
        services.AddCqrs(typeof(DependencyInjection).Assembly);
}
