using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Errors;
using Inventory.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Reservations.Commands;

/// <summary>Bước cuối saga: thanh toán thành công → trừ kho thật.</summary>
public sealed record CommitReservationCommand(Guid OrderId) : ICommand<Unit>;

internal sealed class CommitReservationHandler(
    IReservationRepository reservations,
    IStockRepository stocks,
    IStockMovementRepository movements,
    IUnitOfWork unitOfWork,
    ICacheService cache,
    ILogger<CommitReservationHandler> logger) : ICommandHandler<CommitReservationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(CommitReservationCommand command, CancellationToken ct)
    {
        var reservation = await reservations.GetByOrderIdAsync(command.OrderId, ct);
        if (reservation is null)
            return Result.Failure<Unit>(InventoryErrors.ReservationNotFound(command.OrderId));

        if (reservation.Status == ReservationStatus.Committed)
            return Result.Success(Unit.Value);   // idempotent

        var productIds = reservation.Lines.Select(l => l.ProductId).ToList();
        var items = (await stocks.GetByProductIdsAsync(productIds, ct)).ToDictionary(s => s.ProductId);

        foreach (var line in reservation.Lines)
        {
            if (!items.TryGetValue(line.ProductId, out var stock)) continue;

            stock.CommitReservation(line.Quantity);
            movements.Add(StockMovement.Log(stock.ProductId, stock.Sku, StockMovementType.Outbound,
                line.Quantity, stock.QuantityOnHand, command.OrderId.ToString(), "Xuất kho theo đơn hàng"));
        }

        reservation.Commit();
        await unitOfWork.SaveChangesAsync(ct);

        foreach (var id in productIds) await cache.RemoveAsync(CacheKeys.Stock(id), ct);

        logger.LogInformation("Đã chốt giữ chỗ cho đơn {OrderId}", command.OrderId);
        return Result.Success(Unit.Value);
    }
}

/// <summary>Bồi hoàn: thanh toán thất bại hoặc đơn bị hủy → nhả hàng về trạng thái bán được.</summary>
public sealed record ReleaseReservationCommand(Guid OrderId, string? Reason = null) : ICommand<Unit>;

internal sealed class ReleaseReservationHandler(
    IReservationRepository reservations,
    IStockRepository stocks,
    IStockMovementRepository movements,
    IUnitOfWork unitOfWork,
    ICacheService cache,
    ILogger<ReleaseReservationHandler> logger) : ICommandHandler<ReleaseReservationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ReleaseReservationCommand command, CancellationToken ct)
    {
        var reservation = await reservations.GetByOrderIdAsync(command.OrderId, ct);
        if (reservation is null)
            return Result.Failure<Unit>(InventoryErrors.ReservationNotFound(command.OrderId));

        if (reservation.Status != ReservationStatus.Pending)
            return Result.Success(Unit.Value);   // đã release/commit rồi — không làm gì thêm

        var productIds = reservation.Lines.Select(l => l.ProductId).ToList();
        var items = (await stocks.GetByProductIdsAsync(productIds, ct)).ToDictionary(s => s.ProductId);

        foreach (var line in reservation.Lines)
        {
            if (!items.TryGetValue(line.ProductId, out var stock)) continue;

            stock.ReleaseReservation(line.Quantity);
            movements.Add(StockMovement.Log(stock.ProductId, stock.Sku, StockMovementType.Released,
                line.Quantity, stock.QuantityAvailable, command.OrderId.ToString(),
                command.Reason ?? "Nhả giữ chỗ"));
        }

        reservation.Release(command.Reason);
        await unitOfWork.SaveChangesAsync(ct);

        foreach (var id in productIds) await cache.RemoveAsync(CacheKeys.Stock(id), ct);

        logger.LogInformation("Đã nhả giữ chỗ cho đơn {OrderId}: {Reason}", command.OrderId, command.Reason);
        return Result.Success(Unit.Value);
    }
}
