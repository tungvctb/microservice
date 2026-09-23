using BuildingBlocks.Infrastructure.Kafka;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Idempotency;

public sealed class EfInboxStore<TContext>(TContext db) : IInboxStore
    where TContext : DbContext, IIntegrationDbContext
{
    public Task<bool> HasProcessedAsync(Guid eventId, string consumerName, CancellationToken ct) =>
        db.InboxMessages.AsNoTracking()
            .AnyAsync(m => m.EventId == eventId && m.ConsumerName == consumerName, ct);

    public async Task MarkProcessedAsync(Guid eventId, string consumerName, string eventType, CancellationToken ct)
    {
        db.InboxMessages.Add(new InboxMessage
        {
            EventId = eventId,
            ConsumerName = consumerName,
            EventType = eventType
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Hai instance cùng xử lý 1 event: unique index chặn bản ghi thứ hai — đúng ý đồ, bỏ qua.
        }
    }
}
