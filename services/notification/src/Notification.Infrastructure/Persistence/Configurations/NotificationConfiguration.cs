using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecipientId).HasColumnName("recipient_id");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Link).HasColumnName("link").HasMaxLength(500);
        builder.Property(x => x.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        builder.Property(x => x.ReadAtUtc).HasColumnName("read_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Metadata dạng jsonb: schema linh hoạt, vẫn query được bằng toán tử JSON của Postgres.
        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions)
                     ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                    d => d.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key, kv.Value)),
                    d => new Dictionary<string, string>(d)));

        builder.Property(x => x.Version).HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        // Index phục vụ query chính: thông báo của tôi, mới nhất trước.
        builder.HasIndex(x => new { x.RecipientId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.RecipientId, x.IsRead });

        builder.Ignore(x => x.IsBroadcast);
        builder.Ignore(x => x.DomainEvents);
    }
}
