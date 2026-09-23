using System.Reflection;
using BuildingBlocks.Contracts.IntegrationEvents;

namespace BuildingBlocks.Infrastructure.Kafka;

/// <summary>
/// Ánh xạ chuỗi EventType trong header Kafka về CLR type tương ứng.
/// Nhờ vậy consumer không cần biết trước tên assembly của producer.
/// </summary>
public sealed class IntegrationEventRegistry
{
    private readonly Dictionary<string, Type> _map = new(StringComparer.OrdinalIgnoreCase);

    public IntegrationEventRegistry()
    {
        // Toàn bộ event khai báo trong BuildingBlocks.Contracts đều đăng ký sẵn.
        RegisterFrom(typeof(IntegrationEvent).Assembly);
    }

    public void RegisterFrom(Assembly assembly)
    {
        var eventTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IntegrationEvent).IsAssignableFrom(t));

        foreach (var type in eventTypes)
        {
            if (Activator.CreateInstance(type) is not IntegrationEvent instance) continue;
            _map[instance.EventType] = type;
        }
    }

    public Type? Resolve(string eventType) => _map.GetValueOrDefault(eventType);

    public IReadOnlyDictionary<string, Type> All => _map;
}
