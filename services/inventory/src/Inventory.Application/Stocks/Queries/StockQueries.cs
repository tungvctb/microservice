using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Core.Results;
using Inventory.Application.Common;
using Inventory.Domain.Errors;
using Inventory.Domain.Repositories;

namespace Inventory.Application.Stocks.Queries;

public sealed record GetStockByProductQuery(Guid ProductId) : IQuery<StockItemDto>;

internal sealed class GetStockByProductHandler(IStockRepository stocks, ICacheService cache)
    : IQueryHandler<GetStockByProductQuery, StockItemDto>
{
    public async Task<Result<StockItemDto>> Handle(GetStockByProductQuery query, CancellationToken ct)
    {
        // TTL ngắn (1 phút): tồn kho thay đổi liên tục, cache lâu dễ gây oversell về mặt hiển thị.
        var cached = await cache.GetAsync<StockItemDto>(CacheKeys.Stock(query.ProductId), ct);
        if (cached is not null) return Result.Success(cached);

        var item = await stocks.GetByProductIdAsync(query.ProductId, ct);
        if (item is null) return Result.Failure<StockItemDto>(InventoryErrors.StockNotFound(query.ProductId));

        var dto = item.ToDto();
        await cache.SetAsync(CacheKeys.Stock(query.ProductId), dto, TimeSpan.FromMinutes(1), ct);
        return Result.Success(dto);
    }
}

public sealed record GetStockBatchQuery(IReadOnlyCollection<Guid> ProductIds) : IQuery<IReadOnlyList<StockItemDto>>;

internal sealed class GetStockBatchHandler(IStockRepository stocks)
    : IQueryHandler<GetStockBatchQuery, IReadOnlyList<StockItemDto>>
{
    public async Task<Result<IReadOnlyList<StockItemDto>>> Handle(GetStockBatchQuery query, CancellationToken ct)
    {
        if (query.ProductIds.Count == 0)
            return Result.Success<IReadOnlyList<StockItemDto>>(Array.Empty<StockItemDto>());

        var items = await stocks.GetByProductIdsAsync(query.ProductIds, ct);
        return Result.Success<IReadOnlyList<StockItemDto>>(items.Select(i => i.ToDto()).ToList());
    }
}

public sealed record SearchStockQuery(
    string? Search = null, bool? LowStockOnly = null, string? WarehouseCode = null,
    int Page = 1, int PageSize = 20) : IQuery<PagedResult<StockItemDto>>;

internal sealed class SearchStockHandler(IStockRepository stocks)
    : IQueryHandler<SearchStockQuery, PagedResult<StockItemDto>>
{
    public async Task<Result<PagedResult<StockItemDto>>> Handle(SearchStockQuery query, CancellationToken ct)
    {
        var page = await stocks.SearchAsync(new StockFilter
        {
            Search = query.Search,
            LowStockOnly = query.LowStockOnly,
            WarehouseCode = query.WarehouseCode,
            Page = query.Page,
            PageSize = query.PageSize
        }, ct);

        return Result.Success(new PagedResult<StockItemDto>(
            page.Items.Select(i => i.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount));
    }
}

public sealed record GetLowStockQuery : IQuery<IReadOnlyList<StockItemDto>>;

internal sealed class GetLowStockHandler(IStockRepository stocks)
    : IQueryHandler<GetLowStockQuery, IReadOnlyList<StockItemDto>>
{
    public async Task<Result<IReadOnlyList<StockItemDto>>> Handle(GetLowStockQuery query, CancellationToken ct)
    {
        var items = await stocks.GetLowStockAsync(ct);
        return Result.Success<IReadOnlyList<StockItemDto>>(items.Select(i => i.ToDto()).ToList());
    }
}

public sealed record GetStockMovementsQuery(Guid ProductId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<StockMovementDto>>;

internal sealed class GetStockMovementsHandler(IStockMovementRepository movements)
    : IQueryHandler<GetStockMovementsQuery, PagedResult<StockMovementDto>>
{
    public async Task<Result<PagedResult<StockMovementDto>>> Handle(GetStockMovementsQuery query, CancellationToken ct)
    {
        var page = await movements.GetByProductAsync(query.ProductId,
            new PageRequest { Page = query.Page, PageSize = query.PageSize }, ct);

        return Result.Success(new PagedResult<StockMovementDto>(
            page.Items.Select(m => m.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount));
    }
}
