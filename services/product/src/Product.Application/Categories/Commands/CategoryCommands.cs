using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Product.Application.Common;
using Product.Domain.Entities;
using Product.Domain.Errors;
using Product.Domain.Repositories;

namespace Product.Application.Categories.Commands;

public sealed record CreateCategoryCommand(string Name, string? Description) : ICommand<CategoryDto>;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

internal sealed class CreateCategoryHandler(
    ICategoryRepository categories, IUnitOfWork unitOfWork, ICacheService cache)
    : ICommandHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand command, CancellationToken ct)
    {
        if (await categories.NameExistsAsync(command.Name, ct: ct))
            return Result.Failure<CategoryDto>(CategoryErrors.NameDuplicated(command.Name));

        var category = Category.Create(command.Name, command.Description);
        categories.Add(category);
        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveByPrefixAsync(CacheKeys.CategoryPrefix, ct);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.Slug,
            category.Description, category.IsActive, 0, category.CreatedAtUtc));
    }
}

public sealed record UpdateCategoryCommand(Guid Id, string Name, string? Description, bool IsActive)
    : ICommand<CategoryDto>;

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

internal sealed class UpdateCategoryHandler(
    ICategoryRepository categories, IUnitOfWork unitOfWork, ICacheService cache)
    : ICommandHandler<UpdateCategoryCommand, CategoryDto>
{
    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand command, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(command.Id, ct);
        if (category is null) return Result.Failure<CategoryDto>(CategoryErrors.NotFound(command.Id));

        if (await categories.NameExistsAsync(command.Name, command.Id, ct))
            return Result.Failure<CategoryDto>(CategoryErrors.NameDuplicated(command.Name));

        category.Update(command.Name, command.Description, command.IsActive);
        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveByPrefixAsync(CacheKeys.CategoryPrefix, ct);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.Slug,
            category.Description, category.IsActive, category.Products.Count, category.CreatedAtUtc));
    }
}

public sealed record DeleteCategoryCommand(Guid Id) : ICommand<Unit>;

internal sealed class DeleteCategoryHandler(
    ICategoryRepository categories, IUnitOfWork unitOfWork, ICacheService cache)
    : ICommandHandler<DeleteCategoryCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteCategoryCommand command, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(command.Id, ct);
        if (category is null) return Result.Failure<Unit>(CategoryErrors.NotFound(command.Id));

        if (await categories.HasProductsAsync(command.Id, ct))
            return Result.Failure<Unit>(CategoryErrors.HasProducts);

        categories.Remove(category);
        await unitOfWork.SaveChangesAsync(ct);
        await cache.RemoveByPrefixAsync(CacheKeys.CategoryPrefix, ct);

        return Result.Success(Unit.Value);
    }
}
