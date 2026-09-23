using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Payment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services) =>
        services.AddCqrs(typeof(DependencyInjection).Assembly);
}
