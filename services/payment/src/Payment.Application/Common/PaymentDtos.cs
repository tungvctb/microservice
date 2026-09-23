using Payment.Domain.Entities;
using Payment.Domain.Enums;

namespace Payment.Application.Common;

public sealed record PaymentDto(
    Guid Id, Guid OrderId, Guid CustomerId, decimal Amount, string Currency,
    PaymentMethod Method, PaymentStatus Status, string? TransactionRef,
    string? FailureReason, decimal RefundedAmount, decimal RefundableAmount,
    DateTime CreatedAtUtc, DateTime? CompletedAtUtc, IReadOnlyList<RefundDto> Refunds);

public sealed record RefundDto(Guid Id, decimal Amount, string Currency, string Reason,
    string? TransactionRef, DateTime CreatedAtUtc);

public static class PaymentMapping
{
    public static PaymentDto ToDto(this PaymentTransaction p) => new(
        p.Id, p.OrderId, p.CustomerId, p.Amount.Amount, p.Amount.Currency, p.Method, p.Status,
        p.TransactionRef, p.FailureReason, p.RefundedAmount, p.RefundableAmount,
        p.CreatedAtUtc, p.CompletedAtUtc,
        p.Refunds.Select(r => new RefundDto(r.Id, r.Amount.Amount, r.Amount.Currency,
            r.Reason, r.TransactionRef, r.CreatedAtUtc)).ToList());
}
