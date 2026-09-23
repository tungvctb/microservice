using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Product.Application.Common;
using Product.Domain.Errors;
using Product.Domain.Repositories;

namespace Product.Application.Products.Commands;

public sealed record ChangeProductPriceCommand(Guid Id, decimal NewPrice, string Currency) : ICommand<ProductDto>;

public sealed class ChangeProductPriceValidator : AbstractValidator<ChangeProductPriceCommand>
{
    public ChangeProductPriceValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewPrice).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

internal sealed class ChangeProductPriceHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<ChangeProductPriceCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(ChangeProductPriceCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.Id, ct);
        if (product is null) return Result.Failure<ProductDto>(ProductErrors.NotFound(command.Id));

        product.ChangePrice(command.NewPrice, command.Currency);
        await unitOfWork.SaveChangesAsync(ct);

        await cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, ct);
        return Result.Success(product.ToDto());
    }
}
