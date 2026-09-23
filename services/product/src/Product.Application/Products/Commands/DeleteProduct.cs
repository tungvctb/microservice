using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using Product.Application.Common;
using Product.Domain.Errors;
using Product.Domain.Repositories;

namespace Product.Application.Products.Commands;

public sealed record DeleteProductCommand(Guid Id) : ICommand<Unit>;

internal sealed class DeleteProductHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<DeleteProductCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteProductCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.Id, ct);
        if (product is null) return Result.Failure<Unit>(ProductErrors.NotFound(command.Id));

        // Phát event trước khi xóa để interceptor kịp ghi outbox trong cùng transaction.
        product.MarkDeleted();
        products.Remove(product);
        await unitOfWork.SaveChangesAsync(ct);

        await cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, ct);
        return Result.Success(Unit.Value);
    }
}
