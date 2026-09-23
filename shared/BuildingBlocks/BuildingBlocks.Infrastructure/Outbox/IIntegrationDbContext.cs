using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Outbox;

/// <summary>DbContext của mỗi service implement interface này để dùng chung hạ tầng Outbox/Inbox.</summary>
public interface IIntegrationDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
