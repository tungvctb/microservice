using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Results;
using Product.Domain.Repositories;

namespace Product.Application.Products.Queries;

/// <summary>Batch lookup — Order service gọi qua gRPC để định giá nhiều dòng hàng một lượt.</summary>
public sealed record GetProductsByIdsQuery(IReadOnlyCollection<Guid> Ids) : IQuery<IReadOnlyList<ProductDto>>;

internal sealed class GetProductsByIdsHandler(IProductRepository products)
    : IQueryHandler<GetProductsByIdsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<Result<IReadOnlyList<ProductDto>>> Handle(GetProductsByIdsQuery query, CancellationToken ct)
    {
        if (query.Ids.Count == 0)
            return Result.Success<IReadOnlyList<ProductDto>>(Array.Empty<ProductDto>());

        var items = await products.GetByIdsAsync(query.Ids, ct);
        return Result.Success<IReadOnlyList<ProductDto>>(items.Select(p => p.ToDto()).ToList());
    }
}
