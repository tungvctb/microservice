using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Results;
using Product.Application.Common;
using Product.Domain.Errors;
using Product.Domain.Repositories;

namespace Product.Application.Products.Queries;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<ProductDto>;

internal sealed class GetProductByIdHandler(IProductRepository products, ICacheService cache)
    : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery query, CancellationToken ct)
    {
        // Cache-aside: sản phẩm bị đọc nhiều hơn ghi rất nhiều lần.
        var cached = await cache.GetAsync<ProductDto>(CacheKeys.Product(query.Id), ct);
        if (cached is not null) return Result.Success(cached);

        var product = await products.GetByIdAsync(query.Id, ct);
        if (product is null) return Result.Failure<ProductDto>(ProductErrors.NotFound(query.Id));

        var dto = product.ToDto();
        await cache.SetAsync(CacheKeys.Product(query.Id), dto, TimeSpan.FromMinutes(10), ct);

        return Result.Success(dto);
    }
}
