using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Results;
using Product.Application.Common;
using Product.Domain.Errors;
using Product.Domain.Repositories;

namespace Product.Application.Categories.Queries;

public sealed record GetCategoriesQuery(bool OnlyActive = false) : IQuery<IReadOnlyList<CategoryDto>>;

internal sealed class GetCategoriesHandler(ICategoryRepository categories, ICacheService cache)
    : IQueryHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetCategoriesQuery query, CancellationToken ct)
    {
        var key = CacheKeys.CategoryList(query.OnlyActive);

        var list = await cache.GetOrSetAsync(key, async token =>
        {
            var entities = await categories.GetAllAsync(query.OnlyActive, token);
            return entities.Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description,
                c.IsActive, c.Products.Count, c.CreatedAtUtc)).ToList();
        }, TimeSpan.FromMinutes(15), ct);

        return Result.Success<IReadOnlyList<CategoryDto>>(list);
    }
}

public sealed record GetCategoryByIdQuery(Guid Id) : IQuery<CategoryDto>;

internal sealed class GetCategoryByIdHandler(ICategoryRepository categories)
    : IQueryHandler<GetCategoryByIdQuery, CategoryDto>
{
    public async Task<Result<CategoryDto>> Handle(GetCategoryByIdQuery query, CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(query.Id, ct);
        if (category is null) return Result.Failure<CategoryDto>(CategoryErrors.NotFound(query.Id));

        return Result.Success(new CategoryDto(category.Id, category.Name, category.Slug,
            category.Description, category.IsActive, category.Products.Count, category.CreatedAtUtc));
    }
}
