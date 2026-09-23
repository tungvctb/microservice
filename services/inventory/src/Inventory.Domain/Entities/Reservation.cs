using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;
using Inventory.Domain.Enums;
using Inventory.Domain.Events;

namespace Inventory.Domain.Entities;

/// <summary>
/// Một lần giữ chỗ cho 1 đơn hàng. Có TTL: nếu order không xác nhận kịp,
/// job nền sẽ nhả hàng để không "kẹt" tồn kho vĩnh viễn.
/// </summary>
public sealed class Reservation : AggregateRoot
{
    private readonly List<ReservationLine> _lines = new();

    private Reservation() { }

    public Guid OrderId { get; private set; }
    public ReservationStatus Status { get; private set; } = ReservationStatus.Pending;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? Note { get; private set; }

    public IReadOnlyCollection<ReservationLine> Lines => _lines.AsReadOnly();
    public bool IsExpired => Status == ReservationStatus.Pending && DateTime.UtcNow > ExpiresAtUtc;

    public static Reservation Create(Guid orderId, IEnumerable<(Guid ProductId, int Quantity)> lines, int ttlSeconds)
    {
        var reservation = new Reservation
        {
            OrderId = orderId,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(ttlSeconds <= 0 ? 900 : ttlSeconds)
        };

        foreach (var (productId, quantity) in lines)
            reservation._lines.Add(ReservationLine.Create(reservation.Id, productId, quantity));

        if (reservation._lines.Count == 0)
            throw new DomainException("reservation.empty", "Giữ chỗ phải có ít nhất 1 dòng hàng.");

        reservation.Raise(new StockReservedDomainEvent(orderId, reservation.Id,
            reservation._lines.Select(l => (l.ProductId, l.Quantity)).ToList()));

        return reservation;
    }

    public void Commit()
    {
        EnsurePending();
        Status = ReservationStatus.Committed;
        CompletedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Release(string? reason = null)
    {
        if (Status != ReservationStatus.Pending) return;   // idempotent: gọi lại không gây lỗi
        Status = ReservationStatus.Released;
        CompletedAtUtc = DateTime.UtcNow;
        Note = reason;
        Touch();

        Raise(new StockReleasedDomainEvent(OrderId, Id));
    }

    public void MarkExpired()
    {
        if (Status != ReservationStatus.Pending) return;
        Status = ReservationStatus.Expired;
        CompletedAtUtc = DateTime.UtcNow;
        Note = "Hết hạn giữ chỗ";
        Touch();

        Raise(new StockReleasedDomainEvent(OrderId, Id));
    }

    private void EnsurePending()
    {
        if (Status != ReservationStatus.Pending)
            throw new DomainException("reservation.invalid_state",
                $"Giữ chỗ đang ở trạng thái {Status}, không thể thao tác tiếp.");
    }
}

public sealed class ReservationLine : Entity
{
    private ReservationLine() { }

    public Guid ReservationId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    internal static ReservationLine Create(Guid reservationId, Guid productId, int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("reservation.invalid_quantity", "Số lượng giữ chỗ phải lớn hơn 0.");

        return new ReservationLine { ReservationId = reservationId, ProductId = productId, Quantity = quantity };
    }
}
