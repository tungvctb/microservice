using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Payment.Application.Common;
using Payment.Domain.Errors;
using Payment.Domain.Repositories;

namespace Payment.Application.Payments.Commands;

/// <summary>Trừ tiền cho giao dịch đang ở trạng thái Authorized (điển hình: COD giao thành công).</summary>
public sealed record CapturePaymentCommand(Guid PaymentId) : ICommand<PaymentDto>;

internal sealed class CapturePaymentHandler(IPaymentRepository payments, IUnitOfWork unitOfWork)
    : ICommandHandler<CapturePaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(CapturePaymentCommand command, CancellationToken ct)
    {
        var payment = await payments.GetByIdAsync(command.PaymentId, ct);
        if (payment is null) return Result.Failure<PaymentDto>(PaymentErrors.NotFound(command.PaymentId));

        payment.Capture(payment.TransactionRef ?? $"CAP-{Guid.NewGuid():N}"[..20]);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(payment.ToDto());
    }
}

public sealed record RefundPaymentCommand(Guid PaymentId, decimal Amount, string Reason) : ICommand<PaymentDto>;

public sealed class RefundPaymentValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

internal sealed class RefundPaymentHandler(
    IPaymentRepository payments, IPaymentGateway gateway, IUnitOfWork unitOfWork)
    : ICommandHandler<RefundPaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(RefundPaymentCommand command, CancellationToken ct)
    {
        var payment = await payments.GetByIdAsync(command.PaymentId, ct);
        if (payment is null) return Result.Failure<PaymentDto>(PaymentErrors.NotFound(command.PaymentId));

        var gatewayResult = await gateway.RefundAsync(payment.TransactionRef!, command.Amount, ct);
        if (!gatewayResult.Success)
            return Result.Failure<PaymentDto>(
                PaymentErrors.GatewayDeclined(gatewayResult.FailureReason ?? "Hoàn tiền thất bại."));

        payment.IssueRefund(command.Amount, command.Reason);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(payment.ToDto());
    }
}

/// <summary>Đánh dấu thất bại thủ công — dùng khi vận hành cần can thiệp.</summary>
public sealed record FailPaymentCommand(Guid PaymentId, string Reason) : ICommand<PaymentDto>;

internal sealed class FailPaymentHandler(IPaymentRepository payments, IUnitOfWork unitOfWork)
    : ICommandHandler<FailPaymentCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(FailPaymentCommand command, CancellationToken ct)
    {
        var payment = await payments.GetByIdAsync(command.PaymentId, ct);
        if (payment is null) return Result.Failure<PaymentDto>(PaymentErrors.NotFound(command.PaymentId));

        payment.Fail(command.Reason);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(payment.ToDto());
    }
}
