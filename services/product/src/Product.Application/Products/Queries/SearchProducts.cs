using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Product.Domain.Repositories;

namespace Product.Application.Products.Queries;

public sealed record SearchProductsQuery(
    string? Search = null,
    Guid? CategoryId = null,
    bool? IsActive = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string SortBy = "createdAt",
    bool Descending = true,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ProductSummaryDto>>;

public sealed class SearchProductsValidator : AbstractValidator<SearchProductsQuery>
{
    private static readonly string[] AllowedSorts = { "createdAt", "name", "price", "sku" };

    public SearchProductsValidator()
    {
        RuleFor(x => x.SortBy).Must(s => AllowedSorts.Contains(s))
            .WithMessage($"SortBy chỉ nhận: {string.Join(", ", AllowedSorts)}.");
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue)
            .WithMessage("MaxPrice phải >= MinPrice.");
    }
}

internal sealed class SearchProductsHandler(IProductRepository products)
    : IQueryHandler<SearchProductsQuery, PagedResult<ProductSummaryDto>>
{
    public async Task<Result<PagedResult<ProductSummaryDto>>> Handle(SearchProductsQuery query, CancellationToken ct)
    {
        var filter = new ProductFilter
        {
            Search = query.Search,
            CategoryId = query.CategoryId,
            IsActive = query.IsActive,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            SortBy = query.SortBy,
            Descending = query.Descending,
            Page = query.Page,
            PageSize = query.PageSize
        };

        var page = await products.SearchAsync(filter, ct);

        return Result.Success(new PagedResult<ProductSummaryDto>(
            page.Items.Select(p => p.ToSummary()).ToList(),
            page.Page, page.PageSize, page.TotalCount));
    }
}
