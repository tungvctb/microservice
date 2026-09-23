using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Entities;

namespace Order.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderAggregate>
{
    public void Configure(EntityTypeBuilder<OrderAggregate> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber).HasColumnName("order_number").HasMaxLength(40).IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.CustomerEmail).HasColumnName("customer_email").HasMaxLength(200);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ReservationId).HasColumnName("reservation_id");
        builder.Property(x => x.PaymentId).HasColumnName("payment_id");
        builder.Property(x => x.PaymentRef).HasColumnName("payment_ref").HasMaxLength(60);
        builder.Property(x => x.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(300);
        builder.Property(x => x.ConfirmedAtUtc).HasColumnName("confirmed_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.OwnsOne(x => x.TotalAmount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("total_amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });
        builder.Navigation(x => x.TotalAmount).IsRequired();

        builder.OwnsOne(x => x.ShippingAddress, address =>
        {
            address.Property(a => a.RecipientName).HasColumnName("ship_recipient").HasMaxLength(120).IsRequired();
            address.Property(a => a.Phone).HasColumnName("ship_phone").HasMaxLength(20).IsRequired();
            address.Property(a => a.Street).HasColumnName("ship_street").HasMaxLength(300).IsRequired();
            address.Property(a => a.Ward).HasColumnName("ship_ward").HasMaxLength(100);
            address.Property(a => a.District).HasColumnName("ship_district").HasMaxLength(100);
            address.Property(a => a.City).HasColumnName("ship_city").HasMaxLength(100).IsRequired();
            address.Property(a => a.Note).HasColumnName("ship_note").HasMaxLength(300);
        });
        builder.Navigation(x => x.ShippingAddress).IsRequired();

        builder.Property(x => x.Version).HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ReservationId);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.OwnsOne(x => x.UnitPrice, money =>
        {
            money.Property(m => m.Amount).HasColumnName("unit_price").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });
        builder.Navigation(x => x.UnitPrice).IsRequired();

        builder.HasIndex(x => x.ProductId);
        builder.Ignore(x => x.LineTotal);
    }
}
