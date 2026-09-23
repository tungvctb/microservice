using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Payment.Application.Common;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Repositories;

namespace Payment.Application.Payments.Commands;

public sealed record ProcessPaymentCommand(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string IdempotencyKey) : ICommand<PaymentDto>;

public sealed class ProcessPaymentValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}

/// <summary>
/// Bước 2 của saga. Idempotent hai lớp: theo IdempotencyKey và theo OrderId —
/// gọi lại bao nhiêu lần cũng chỉ có đúng 1 giao dịch cho mỗi đơn.
/// </summary>
internal sealed class ProcessPaymentHandler(
    IPaymentRepository payments,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    ILogger<ProcessPaymentHandler> logger) : ICommandHandler<ProcessPaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(ProcessPaymentCommand command, CancellationToken ct)
    {
        var existing = await payments.GetByIdempotencyKeyAsync(command.IdempotencyKey, ct)
                       ?? await payments.GetByOrderIdAsync(command.OrderId, ct);

        if (existing is not null)
        {
            logger.LogInformation("Bỏ qua xử lý trùng cho đơn {OrderId} — đã có giao dịch {PaymentId} ({Status})",
                command.OrderId, existing.Id, existing.Status);
            return Result.Success(existing.ToDto());
        }

        var payment = PaymentTransaction.Create(command.OrderId, command.CustomerId,
            command.Amount, command.Currency, command.Method, command.IdempotencyKey);

        payments.Add(payment);

        // COD chưa thu tiền lúc đặt hàng — chỉ ghi nhận, capture khi giao xong.
        if (command.Method == PaymentMethod.Cod)
        {
            payment.Authorize($"COD-{payment.Id:N}"[..20]);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(payment.ToDto());
        }

        var gatewayResult = await gateway.ChargeAsync(new GatewayChargeRequest(
            command.OrderId, command.CustomerId, command.Amount, command.Currency,
            command.Method.ToString(), command.IdempotencyKey), ct);

        if (gatewayResult.Success)
            payment.Capture(gatewayResult.TransactionRef!);
        else
            payment.Fail(gatewayResult.FailureReason ?? "Cổng thanh toán từ chối giao dịch.");

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Thanh toán đơn {OrderId}: {Status}", command.OrderId, payment.Status);
        return Result.Success(payment.ToDto());
    }
}
