using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Notification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services) =>
        services.AddCqrs(typeof(DependencyInjection).Assembly);
}
