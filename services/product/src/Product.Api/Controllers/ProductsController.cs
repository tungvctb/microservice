using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Application.Products;
using Product.Application.Products.Commands;
using Product.Application.Products.Queries;

namespace Product.Api.Controllers;

[Route("api/products")]
[Produces("application/json")]
public sealed class ProductsController : ApiControllerBase
{
    /// <summary>Danh sách sản phẩm có phân trang, tìm kiếm và lọc.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] SearchProductsQuery query, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetProductByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var result = await Dispatcher.Send(command, ct);
        return result.IsSuccess
            ? CreatedResponse(result, nameof(GetById), new { id = result.Value.Id })
            : ToResponse(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(
            new UpdateProductCommand(id, request.Name, request.Description, request.CategoryId, request.ImageUrl), ct));

    /// <summary>Đổi giá — tách riêng vì phát ra integration event mà Order service theo dõi.</summary>
    [HttpPatch("{id:guid}/price")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePrice(Guid id, [FromBody] ChangePriceRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(
            new ChangeProductPriceCommand(id, request.NewPrice, request.Currency), ct));

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetStatusRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new SetProductStatusCommand(id, request.IsActive), ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new DeleteProductCommand(id), ct));
}

public sealed record UpdateProductRequest(string Name, string? Description, Guid CategoryId, string? ImageUrl);
public sealed record ChangePriceRequest(decimal NewPrice, string Currency = "VND");
public sealed record SetStatusRequest(bool IsActive);
