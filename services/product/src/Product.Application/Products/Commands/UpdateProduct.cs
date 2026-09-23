using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Product.Application.Common;
using Product.Domain.Errors;
using Product.Domain.Repositories;

namespace Product.Application.Products.Commands;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    Guid CategoryId,
    string? ImageUrl) : ICommand<ProductDto>;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

internal sealed class UpdateProductHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<UpdateProductCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand command, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(command.Id, ct);
        if (product is null) return Result.Failure<ProductDto>(ProductErrors.NotFound(command.Id));

        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null || !category.IsActive)
            return Result.Failure<ProductDto>(ProductErrors.InvalidCategory);

        product.UpdateDetails(command.Name, command.Description, command.CategoryId, command.ImageUrl);
        await unitOfWork.SaveChangesAsync(ct);

        await cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, ct);
        return Result.Success(product.ToDto() with { CategoryName = category.Name });
    }
}
