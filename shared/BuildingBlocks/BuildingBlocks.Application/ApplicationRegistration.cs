using System.Reflection;
using BuildingBlocks.Application.Cqrs;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application;

public static class ApplicationRegistration
{
    /// <summary>Quét assembly và đăng ký toàn bộ command/query handler + validator.</summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        var openHandlers = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>), typeof(IDomainEventHandler<>) };

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var itf in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                if (!openHandlers.Contains(itf.GetGenericTypeDefinition())) continue;
                services.AddScoped(itf, type);
            }
        }

        return services;
    }
}
