using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using Product.Application.Common;
using Product.Domain.Errors;
using Product.Domain.Repositories;

namespace Product.Application.Products.Commands;

public sealed record SetProductStatusCommand(Guid Id, bool IsActive) : ICommand<ProductDto>;

internal sealed class SetProductStatusHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<SetProductStatusCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(SetProductStatusCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.Id, ct);
        if (product is null) return Result.Failure<ProductDto>(ProductErrors.NotFound(command.Id));

        product.SetActive(command.IsActive);
        await unitOfWork.SaveChangesAsync(ct);

        await cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, ct);
        return Result.Success(product.ToDto());
    }
}
