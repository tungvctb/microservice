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

namespace Inventory.Application.Stocks.Commands;

public sealed record CreateStockItemCommand(
    Guid ProductId, string Sku, int InitialQuantity, int ReorderLevel, string WarehouseCode = "MAIN")
    : ICommand<StockItemDto>;

public sealed class CreateStockItemValidator : AbstractValidator<CreateStockItemCommand>
{
    public CreateStockItemValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.InitialQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateStockItemHandler(
    IStockRepository stocks, IStockMovementRepository movements,
    IUnitOfWork unitOfWork, ICacheService cache)
    : ICommandHandler<CreateStockItemCommand, StockItemDto>
{
    public async Task<Result<StockItemDto>> Handle(CreateStockItemCommand command, CancellationToken ct)
    {
        if (await stocks.GetByProductIdAsync(command.ProductId, ct) is not null)
            return Result.Failure<StockItemDto>(InventoryErrors.DuplicateStockItem);

        var item = StockItem.Create(command.ProductId, command.Sku, command.InitialQuantity,
            command.ReorderLevel, command.WarehouseCode);

        stocks.Add(item);

        if (command.InitialQuantity > 0)
            movements.Add(StockMovement.Log(item.ProductId, item.Sku, StockMovementType.Inbound,
                command.InitialQuantity, item.QuantityOnHand, null, "Khởi tạo tồn kho"));

        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Stock(command.ProductId), ct);

        return Result.Success(item.ToDto());
    }
}

public sealed record ReceiveStockCommand(Guid ProductId, int Quantity, string? Reference, string? Note)
    : ICommand<StockItemDto>;

public sealed class ReceiveStockValidator : AbstractValidator<ReceiveStockCommand>
{
    public ReceiveStockValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng nhập phải lớn hơn 0.");
    }
}

internal sealed class ReceiveStockHandler(
    IStockRepository stocks, IStockMovementRepository movements,
    IUnitOfWork unitOfWork, ICacheService cache, IDistributedLock distributedLock)
    : ICommandHandler<ReceiveStockCommand, StockItemDto>
{
    public async Task<Result<StockItemDto>> Handle(ReceiveStockCommand command, CancellationToken ct)
    {
        await using var handle = await distributedLock.AcquireAsync(
            CacheKeys.StockLock(command.ProductId), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), ct);

        if (handle is null) return Result.Failure<StockItemDto>(InventoryErrors.LockNotAcquired);

        var item = await stocks.GetByProductIdAsync(command.ProductId, ct);
        if (item is null) return Result.Failure<StockItemDto>(InventoryErrors.StockNotFound(command.ProductId));

        item.Receive(command.Quantity);
        movements.Add(StockMovement.Log(item.ProductId, item.Sku, StockMovementType.Inbound,
            command.Quantity, item.QuantityOnHand, command.Reference, command.Note ?? "Nhập kho"));

        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Stock(command.ProductId), ct);

        return Result.Success(item.ToDto());
    }
}

public sealed record AdjustStockCommand(Guid ProductId, int NewQuantityOnHand, string Reason)
    : ICommand<StockItemDto>;

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.NewQuantityOnHand).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

internal sealed class AdjustStockHandler(
    IStockRepository stocks, IStockMovementRepository movements,
    IUnitOfWork unitOfWork, ICacheService cache, IDistributedLock distributedLock)
    : ICommandHandler<AdjustStockCommand, StockItemDto>
{
    public async Task<Result<StockItemDto>> Handle(AdjustStockCommand command, CancellationToken ct)
    {
        await using var handle = await distributedLock.AcquireAsync(
            CacheKeys.StockLock(command.ProductId), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), ct);

        if (handle is null) return Result.Failure<StockItemDto>(InventoryErrors.LockNotAcquired);

        var item = await stocks.GetByProductIdAsync(command.ProductId, ct);
        if (item is null) return Result.Failure<StockItemDto>(InventoryErrors.StockNotFound(command.ProductId));

        var delta = command.NewQuantityOnHand - item.QuantityOnHand;
        item.Adjust(command.NewQuantityOnHand, command.Reason);

        movements.Add(StockMovement.Log(item.ProductId, item.Sku, StockMovementType.Adjustment,
            delta, item.QuantityOnHand, null, command.Reason));

        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Stock(command.ProductId), ct);

        return Result.Success(item.ToDto());
    }
}

public sealed record SetReorderLevelCommand(Guid ProductId, int ReorderLevel) : ICommand<StockItemDto>;

internal sealed class SetReorderLevelHandler(
    IStockRepository stocks, IUnitOfWork unitOfWork, ICacheService cache)
    : ICommandHandler<SetReorderLevelCommand, StockItemDto>
{
    public async Task<Result<StockItemDto>> Handle(SetReorderLevelCommand command, CancellationToken ct)
    {
        var item = await stocks.GetByProductIdAsync(command.ProductId, ct);
        if (item is null) return Result.Failure<StockItemDto>(InventoryErrors.StockNotFound(command.ProductId));

        item.SetReorderLevel(command.ReorderLevel);
        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Stock(command.ProductId), ct);

        return Result.Success(item.ToDto());
    }
}
