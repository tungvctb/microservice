using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Product.Application.Common;
using Product.Domain.Entities;
using Product.Domain.Errors;
using Product.Domain.Repositories;

namespace Product.Application.Products.Commands;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string? ImageUrl,
    int InitialStock) : ICommand<ProductDto>;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9-_]+$").WithMessage("SKU chỉ gồm chữ, số, '-' và '_'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Giá phải lớn hơn 0.");
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateProductHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<CreateProductCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand command, CancellationToken ct)
    {
        if (await products.SkuExistsAsync(command.Sku, ct: ct))
            return Result.Failure<ProductDto>(ProductErrors.SkuDuplicated(command.Sku));

        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null || !category.IsActive)
            return Result.Failure<ProductDto>(ProductErrors.InvalidCategory);

        var product = ProductItem.Create(command.Sku, command.Name, command.Description,
            command.Price, command.Currency, command.CategoryId, command.ImageUrl, command.InitialStock);

        products.Add(product);
        await unitOfWork.SaveChangesAsync(ct);

        await cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, ct);

        // Category được nạp sẵn để DTO có CategoryName mà không cần query lại.
        return Result.Success(product.ToDto() with { CategoryName = category.Name });
    }
}
