using System.Globalization;
using BuildingBlocks.Application.Cqrs;
using Grpc.Core;
using Payment.Application.Payments.Commands;
using Payment.Application.Payments.Queries;
using Payment.Domain.Enums;
using Common = BuildingBlocks.Contracts.Grpc.Common;
using Proto = BuildingBlocks.Contracts.Grpc.Payment;

namespace Payment.Api.Grpc;

public sealed class PaymentGrpcEndpoint(IDispatcher dispatcher)
    : Proto.PaymentGrpcService.PaymentGrpcServiceBase
{
    public override async Task<Proto.PaymentReply> AuthorizePayment(Proto.AuthorizePaymentRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId) ||
            !Guid.TryParse(request.CustomerId, out var customerId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "orderId/customerId không hợp lệ."));

        if (!decimal.TryParse(request.Amount?.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Số tiền không hợp lệ."));

        var method = Enum.TryParse<PaymentMethod>(request.Method, true, out var parsed)
            ? parsed
            : PaymentMethod.CreditCard;

        var result = await dispatcher.Send(new ProcessPaymentCommand(
            orderId, customerId, amount, request.Amount!.Currency, method,
            request.IdempotencyKey), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error.Message));

        return ToReply(result.Value);
    }

    public override async Task<Proto.PaymentReply> CapturePayment(Proto.PaymentActionRequest request,
        ServerCallContext context)
    {
        var result = await dispatcher.Send(
            new CapturePaymentCommand(Guid.Parse(request.PaymentId)), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error.Message));

        return ToReply(result.Value);
    }

    public override async Task<Proto.PaymentReply> RefundPayment(Proto.RefundRequest request,
        ServerCallContext context)
    {
        if (!decimal.TryParse(request.Amount?.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Số tiền hoàn không hợp lệ."));

        var result = await dispatcher.Send(new RefundPaymentCommand(
            Guid.Parse(request.PaymentId), amount, request.Reason), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error.Message));

        return ToReply(result.Value);
    }

    public override async Task<Proto.PaymentReply> GetPaymentByOrder(Proto.GetPaymentByOrderRequest request,
        ServerCallContext context)
    {
        var result = await dispatcher.Ask(
            new GetPaymentByOrderQuery(Guid.Parse(request.OrderId)), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        return ToReply(result.Value);
    }

    private static Proto.PaymentReply ToReply(Application.Common.PaymentDto p) => new()
    {
        PaymentId = p.Id.ToString(),
        OrderId = p.OrderId.ToString(),
        Status = p.Status.ToString(),
        Amount = new Common.Money
        {
            Amount = p.Amount.ToString(CultureInfo.InvariantCulture),
            Currency = p.Currency
        },
        Method = p.Method.ToString(),
        TransactionRef = p.TransactionRef ?? string.Empty,
        FailureReason = p.FailureReason ?? string.Empty,
        CreatedAt = p.CreatedAtUtc.ToString("O")
    };
}
