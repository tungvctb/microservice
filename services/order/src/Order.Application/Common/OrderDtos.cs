using Order.Domain.Entities;
using Order.Domain.Enums;

namespace Order.Application.Common;

public sealed record OrderDto(
    Guid Id, string OrderNumber, Guid CustomerId, string CustomerEmail,
    OrderStatus Status, PaymentMethodType PaymentMethod,
    decimal TotalAmount, string Currency,
    AddressDto ShippingAddress, IReadOnlyList<OrderLineDto> Lines,
    Guid? ReservationId, Guid? PaymentId, string? PaymentRef, string? CancellationReason,
    DateTime CreatedAtUtc, DateTime? ConfirmedAtUtc, DateTime? CompletedAtUtc);

public sealed record OrderSummaryDto(
    Guid Id, string OrderNumber, OrderStatus Status, decimal TotalAmount, string Currency,
    int ItemCount, DateTime CreatedAtUtc);

public sealed record OrderLineDto(
    Guid ProductId, string Sku, string ProductName, int Quantity,
    decimal UnitPrice, decimal LineTotal, string Currency);

public sealed record AddressDto(
    string RecipientName, string Phone, string Street, string Ward,
    string District, string City, string? Note);

public static class OrderMapping
{
    public static OrderDto ToDto(this OrderAggregate o) => new(
        o.Id, o.OrderNumber, o.CustomerId, o.CustomerEmail, o.Status, o.PaymentMethod,
        o.TotalAmount.Amount, o.TotalAmount.Currency,
        new AddressDto(o.ShippingAddress.RecipientName, o.ShippingAddress.Phone, o.ShippingAddress.Street,
            o.ShippingAddress.Ward, o.ShippingAddress.District, o.ShippingAddress.City, o.ShippingAddress.Note),
        o.Lines.Select(l => new OrderLineDto(l.ProductId, l.Sku, l.ProductName, l.Quantity,
            l.UnitPrice.Amount, l.LineTotal.Amount, l.UnitPrice.Currency)).ToList(),
        o.ReservationId, o.PaymentId, o.PaymentRef, o.CancellationReason,
        o.CreatedAtUtc, o.ConfirmedAtUtc, o.CompletedAtUtc);

    public static OrderSummaryDto ToSummary(this OrderAggregate o) => new(
        o.Id, o.OrderNumber, o.Status, o.TotalAmount.Amount, o.TotalAmount.Currency,
        o.Lines.Sum(l => l.Quantity), o.CreatedAtUtc);
}
