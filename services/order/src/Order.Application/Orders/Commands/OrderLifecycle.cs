using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Order.Application.Common;
using Order.Domain.Errors;
using Order.Domain.Repositories;

namespace Order.Application.Orders.Commands;

/// <summary>Thanh toán thành công → xác nhận đơn. Gọi từ handler nghe PaymentSucceeded.</summary>
public sealed record ConfirmOrderPaymentCommand(Guid OrderId, Guid PaymentId, string PaymentRef)
    : ICommand<OrderDto>;

internal sealed class ConfirmOrderPaymentHandler(
    IOrderRepository orders, IUnitOfWork unitOfWork, ILogger<ConfirmOrderPaymentHandler> logger)
    : ICommandHandler<ConfirmOrderPaymentCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(ConfirmOrderPaymentCommand command, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(command.OrderId, ct);
        if (order is null) return Result.Failure<OrderDto>(OrderErrors.NotFound(command.OrderId));

        order.MarkPaid(command.PaymentId, command.PaymentRef);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Đơn {OrderNumber} đã xác nhận thanh toán", order.OrderNumber);
        return Result.Success(order.ToDto());
    }
}

/// <param name="BySystem">
/// true khi lệnh phát sinh từ saga (handler Kafka) chứ không từ một người dùng đăng nhập —
/// lúc đó không có ICurrentUser để kiểm tra quyền.
/// </param>
public sealed record CancelOrderCommand(Guid OrderId, string Reason, bool BySystem = false)
    : ICommand<OrderDto>;

public sealed class CancelOrderValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

/// <summary>
/// Hủy đơn. Không gọi Inventory trực tiếp ở đây: event OrderCancelled đi qua Kafka
/// và Inventory tự nhả giữ chỗ. Nhờ vậy hủy đơn vẫn thành công kể cả khi Inventory đang chết.
/// </summary>
internal sealed class CancelOrderHandler(
    IOrderRepository orders, IUnitOfWork unitOfWork, ICurrentUser currentUser,
    ILogger<CancelOrderHandler> logger) : ICommandHandler<CancelOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(CancelOrderCommand command, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(command.OrderId, ct);
        if (order is null) return Result.Failure<OrderDto>(OrderErrors.NotFound(command.OrderId));

        if (!command.BySystem)
        {
            var isStaff = currentUser.Roles.Any(r => r is "Admin" or "Manager");
            if (!isStaff && order.CustomerId != currentUser.UserId)
                return Result.Failure<OrderDto>(OrderErrors.Forbidden);
        }

        order.Cancel(command.Reason);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Đã hủy đơn {OrderNumber}: {Reason}", order.OrderNumber, command.Reason);
        return Result.Success(order.ToDto());
    }
}

public sealed record ShipOrderCommand(Guid OrderId) : ICommand<OrderDto>;

internal sealed class ShipOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<ShipOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(ShipOrderCommand command, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(command.OrderId, ct);
        if (order is null) return Result.Failure<OrderDto>(OrderErrors.NotFound(command.OrderId));

        order.MarkShipped();
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(order.ToDto());
    }
}

public sealed record CompleteOrderCommand(Guid OrderId) : ICommand<OrderDto>;

internal sealed class CompleteOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<CompleteOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(CompleteOrderCommand command, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(command.OrderId, ct);
        if (order is null) return Result.Failure<OrderDto>(OrderErrors.NotFound(command.OrderId));

        order.Complete();
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(order.ToDto());
    }
}
