using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Order.Application.Abstractions;
using Order.Application.Common;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Errors;
using Order.Domain.Repositories;
using Order.Domain.ValueObjects;

namespace Order.Application.Orders.Commands;

public sealed record PlaceOrderCommand(
    IReadOnlyList<OrderLineInput> Lines,
    AddressInput ShippingAddress,
    PaymentMethodType PaymentMethod,
    string? CustomerEmail = null) : ICommand<OrderDto>;

public sealed record OrderLineInput(Guid ProductId, int Quantity);

public sealed record AddressInput(
    string RecipientName, string Phone, string Street,
    string Ward, string District, string City, string? Note);

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Đơn hàng phải có ít nhất 1 sản phẩm.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
        });

        RuleFor(x => x.ShippingAddress).NotNull();
        RuleFor(x => x.ShippingAddress.RecipientName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ShippingAddress.Phone).NotEmpty().Matches(@"^[0-9+\-\s]{8,15}$")
            .WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.ShippingAddress.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ShippingAddress.City).NotEmpty().MaximumLength(100);
    }
}

/// <summary>
/// Điều phối saga đặt hàng, 2 bước đồng bộ + phần còn lại bất đồng bộ:
///   1. gRPC → Catalog: xác thực sản phẩm và CHỐT GIÁ tại server (không tin giá client gửi lên).
///   2. gRPC → Inventory: giữ chỗ all-or-nothing.
///   3. Lưu đơn + ghi outbox OrderPlaced trong cùng transaction.
///   4. Payment service nghe OrderPlaced qua Kafka và xử lý tiếp (xem OrderPlacedHandler bên Payment).
/// Nếu bước 2 hỏng sau khi bước 1 xong thì chưa có gì để bồi hoàn — đó là lý do
/// giữ chỗ kho phải đứng SAU định giá và TRƯỚC khi ghi đơn.
/// </summary>
internal sealed class PlaceOrderHandler(
    IOrderRepository orders,
    ICatalogGateway catalog,
    IInventoryGateway inventory,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ILogger<PlaceOrderHandler> logger) : ICommandHandler<PlaceOrderCommand, OrderDto>
{
    private const int ReservationTtlSeconds = 900;   // 15 phút để hoàn tất thanh toán

    public async Task<Result<OrderDto>> Handle(PlaceOrderCommand command, CancellationToken ct)
    {
        var customerId = currentUser.UserId;
        if (customerId is null) return Result.Failure<OrderDto>(OrderErrors.Forbidden);

        // Gộp trùng sản phẩm ngay từ đầu để tránh đơn có 2 dòng cùng 1 SKU.
        var mergedLines = command.Lines
            .GroupBy(l => l.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity)))
            .ToList();

        // --- Bước 1: định giá tại Catalog ---
        CatalogValidationResult pricing;
        try
        {
            pricing = await catalog.ValidateAndPriceAsync(mergedLines, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gọi Catalog thất bại khi đặt hàng");
            return Result.Failure<OrderDto>(OrderErrors.CatalogUnavailable(ex.Message));
        }

        if (!pricing.IsValid)
            return Result.Failure<OrderDto>(
                OrderErrors.ProductValidationFailed(string.Join(" | ", pricing.Errors)));

        var address = ShippingAddress.Create(
            command.ShippingAddress.RecipientName, command.ShippingAddress.Phone,
            command.ShippingAddress.Street, command.ShippingAddress.Ward,
            command.ShippingAddress.District, command.ShippingAddress.City,
            command.ShippingAddress.Note);

        var currency = pricing.PricedLines.FirstOrDefault()?.Currency ?? "VND";

        var order = OrderAggregate.Create(customerId.Value,
            command.CustomerEmail ?? currentUser.Email ?? string.Empty,
            address, command.PaymentMethod, currency);

        foreach (var line in pricing.PricedLines)
            order.AddLine(line.ProductId, line.Sku, line.Name, line.Quantity, line.UnitPrice, line.Currency);

        // --- Bước 2: giữ chỗ kho ---
        ReserveResult reservation;
        try
        {
            reservation = await inventory.ReserveAsync(order.Id, mergedLines, ReservationTtlSeconds, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gọi Inventory thất bại khi giữ chỗ cho đơn {OrderId}", order.Id);
            return Result.Failure<OrderDto>(OrderErrors.InventoryUnavailable(ex.Message));
        }

        if (!reservation.Success)
        {
            logger.LogInformation("Đặt hàng thất bại do thiếu hàng: {Message}", reservation.Message);
            return Result.Failure<OrderDto>(OrderErrors.OutOfStock(reservation.Message));
        }

        // --- Bước 3: ghi đơn + outbox trong 1 transaction ---
        order.MarkStockReserved(reservation.ReservationId!.Value);
        orders.Add(order);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Đã giữ chỗ mà không ghi được đơn -> phải nhả ngay, nếu không hàng bị treo 15 phút.
            logger.LogError(ex, "Lưu đơn {OrderId} thất bại — đang nhả giữ chỗ", order.Id);
            await inventory.ReleaseAsync(order.Id, CancellationToken.None);
            throw;
        }

        logger.LogInformation("Đã tạo đơn {OrderNumber} ({Total} {Currency}) — chờ thanh toán",
            order.OrderNumber, order.TotalAmount.Amount, order.TotalAmount.Currency);

        return Result.Success(order.ToDto());
    }
}
