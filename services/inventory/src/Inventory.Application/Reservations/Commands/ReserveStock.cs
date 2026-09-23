using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Errors;
using Inventory.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Reservations.Commands;

public sealed record ReserveStockCommand(
    Guid OrderId,
    IReadOnlyList<ReserveLineInput> Lines,
    int TtlSeconds = 900) : ICommand<ReserveStockResult>;

public sealed record ReserveLineInput(Guid ProductId, int Quantity);

public sealed class ReserveStockValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Phải có ít nhất 1 dòng hàng.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

/// <summary>
/// Bước 1 của saga đặt hàng. Toàn bộ dòng hàng được giữ chỗ theo kiểu all-or-nothing:
/// thiếu 1 dòng là hủy cả lô, tránh đơn hàng "giữ được một nửa".
/// Khóa phân tán theo từng productId (sắp xếp trước) để tránh deadlock khi nhiều đơn
/// cùng tranh chấp các sản phẩm giao nhau.
/// </summary>
internal sealed class ReserveStockHandler(
    IStockRepository stocks,
    IReservationRepository reservations,
    IStockMovementRepository movements,
    IUnitOfWork unitOfWork,
    IDistributedLock distributedLock,
    ICacheService cache,
    ILogger<ReserveStockHandler> logger) : ICommandHandler<ReserveStockCommand, ReserveStockResult>
{
    public async Task<Result<ReserveStockResult>> Handle(ReserveStockCommand command, CancellationToken ct)
    {
        // Gọi lại cùng OrderId trả về kết quả cũ — an toàn khi Order service retry.
        var existing = await reservations.GetByOrderIdAsync(command.OrderId, ct);
        if (existing is { Status: ReservationStatus.Pending or ReservationStatus.Committed })
        {
            return Result.Success(new ReserveStockResult(true, existing.Id,
                "Đơn hàng đã được giữ chỗ trước đó.", Array.Empty<InsufficientStockDto>()));
        }

        var orderedIds = command.Lines.Select(l => l.ProductId).Distinct().OrderBy(id => id).ToList();
        var locks = new List<IAsyncDisposable>();

        try
        {
            foreach (var productId in orderedIds)
            {
                var handle = await distributedLock.AcquireAsync(
                    CacheKeys.StockLock(productId),
                    expiry: TimeSpan.FromSeconds(15),
                    wait: TimeSpan.FromSeconds(5), ct);

                if (handle is null)
                {
                    logger.LogWarning("Không lấy được khóa tồn kho cho sản phẩm {ProductId}", productId);
                    return Result.Failure<ReserveStockResult>(InventoryErrors.LockNotAcquired);
                }

                locks.Add(handle);
            }

            var items = await stocks.GetByProductIdsAsync(orderedIds, ct);
            var byProduct = items.ToDictionary(s => s.ProductId);

            // Kiểm tra đủ hàng cho TẤT CẢ dòng trước khi động vào bất kỳ dòng nào.
            var insufficient = new List<InsufficientStockDto>();
            foreach (var line in command.Lines)
            {
                if (!byProduct.TryGetValue(line.ProductId, out var stock))
                {
                    insufficient.Add(new InsufficientStockDto(line.ProductId, line.Quantity, 0));
                    continue;
                }

                if (stock.QuantityAvailable < line.Quantity)
                    insufficient.Add(new InsufficientStockDto(line.ProductId, line.Quantity, stock.QuantityAvailable));
            }

            if (insufficient.Count > 0)
            {
                var message = "Không đủ hàng: " + string.Join("; ",
                    insufficient.Select(i => $"{i.ProductId} cần {i.Requested}, còn {i.Available}"));

                logger.LogInformation("Giữ chỗ cho đơn {OrderId} thất bại — {Message}", command.OrderId, message);
                return Result.Success(new ReserveStockResult(false, null, message, insufficient));
            }

            var reservation = Reservation.Create(
                command.OrderId,
                command.Lines.Select(l => (l.ProductId, l.Quantity)),
                command.TtlSeconds);

            foreach (var line in command.Lines)
            {
                var stock = byProduct[line.ProductId];
                if (!stock.TryReserve(line.Quantity))
                    return Result.Failure<ReserveStockResult>(
                        InventoryErrors.InsufficientStock($"Sản phẩm {line.ProductId} vừa hết hàng."));

                movements.Add(StockMovement.Log(stock.ProductId, stock.Sku, StockMovementType.Reserved,
                    line.Quantity, stock.QuantityAvailable, command.OrderId.ToString(), "Giữ chỗ cho đơn hàng"));
            }

            reservations.Add(reservation);
            await unitOfWork.SaveChangesAsync(ct);

            foreach (var productId in orderedIds)
                await cache.RemoveAsync(CacheKeys.Stock(productId), ct);

            logger.LogInformation("Đã giữ chỗ {ReservationId} cho đơn {OrderId} ({Lines} dòng)",
                reservation.Id, command.OrderId, command.Lines.Count);

            return Result.Success(new ReserveStockResult(true, reservation.Id,
                "Giữ chỗ thành công.", Array.Empty<InsufficientStockDto>()));
        }
        finally
        {
            foreach (var handle in locks) await handle.DisposeAsync();
        }
    }
}
