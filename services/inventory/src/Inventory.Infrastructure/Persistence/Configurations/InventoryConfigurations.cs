using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(50).IsRequired();
        builder.Property(x => x.WarehouseCode).HasColumnName("warehouse_code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.QuantityOnHand).HasColumnName("quantity_on_hand");
        builder.Property(x => x.QuantityReserved).HasColumnName("quantity_reserved");
        builder.Property(x => x.ReorderLevel).HasColumnName("reorder_level");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Chống lost-update: hai request cùng trừ kho thì request "chậm chân" sẽ fail và phải đọc lại.
        builder.Property(x => x.Version).HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        builder.HasIndex(x => new { x.ProductId, x.WarehouseCode }).IsUnique();
        builder.HasIndex(x => x.Sku);

        builder.Ignore(x => x.QuantityAvailable);
        builder.Ignore(x => x.IsLowStock);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(300);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Property(x => x.Version).HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        // Một đơn hàng chỉ có tối đa 1 giữ chỗ -> chặn giữ chỗ trùng ở tầng DB.
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ExpiresAtUtc });

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class ReservationLineConfiguration : IEntityTypeConfiguration<ReservationLine>
{
    public void Configure(EntityTypeBuilder<ReservationLine> builder)
    {
        builder.ToTable("reservation_lines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReservationId).HasColumnName("reservation_id");
        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => x.ProductId);
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(50);
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.QuantityAfter).HasColumnName("quantity_after");
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(300);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.ProductId, x.CreatedAtUtc });
        builder.HasIndex(x => x.Reference);
    }
}
