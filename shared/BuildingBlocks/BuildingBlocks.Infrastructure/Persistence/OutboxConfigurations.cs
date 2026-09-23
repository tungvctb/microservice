using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Persistence;

public sealed class OutboxMessageConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Topic).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AggregateId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        // Index phục vụ đúng query của OutboxProcessor: chưa xử lý, sắp theo thời điểm phát sinh.
        builder.HasIndex(x => new { x.ProcessedOnUtc, x.OccurredOnUtc });
        builder.HasIndex(x => x.EventId).IsUnique();
    }
}

public sealed class InboxMessageConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConsumerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();

        // Chặn xử lý trùng ở tầng DB, kể cả khi nhiều instance chạy song song.
        builder.HasIndex(x => new { x.EventId, x.ConsumerName }).IsUnique();
    }
}

public static class IntegrationModelBuilderExtensions
{
    /// <summary>Gắn bảng outbox/inbox vào model của service.</summary>
    public static ModelBuilder ApplyIntegrationTables(this ModelBuilder builder)
    {
        builder.ApplyConfiguration(new OutboxMessageConfiguration());
        builder.ApplyConfiguration(new InboxMessageConfiguration());
        return builder;
    }
}
