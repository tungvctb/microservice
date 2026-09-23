using BuildingBlocks.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>Tự set UpdatedAtUtc cho entity bị sửa — không cần nhớ gọi thủ công trong từng handler.</summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        foreach (var entry in context.ChangeTracker.Entries<Entity>().Where(e => e.State == EntityState.Modified))
            entry.Property(nameof(Entity.UpdatedAtUtc)).CurrentValue = DateTime.UtcNow;

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
