using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Web;
using Inventory.Application.Common;
using Inventory.Application.Stocks.Commands;
using Inventory.Application.Stocks.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[Route("api/stocks")]
[Produces("application/json")]
public sealed class StocksController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] SearchStockQuery query, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(query, ct));

    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StockItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStock(CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetLowStockQuery(), ct));

    [HttpGet("product/{productId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StockItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByProduct(Guid productId, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetStockByProductQuery(productId), ct));

    [HttpGet("product/{productId:guid}/movements")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockMovementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovements(Guid productId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => ToResponse(await Dispatcher.Ask(new GetStockMovementsQuery(productId, page, pageSize), ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<StockItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateStockItemCommand command, CancellationToken ct)
    {
        var result = await Dispatcher.Send(command, ct);
        return result.IsSuccess
            ? CreatedResponse(result, nameof(GetByProduct), new { productId = result.Value.ProductId })
            : ToResponse(result);
    }

    /// <summary>Nhập kho.</summary>
    [HttpPost("product/{productId:guid}/receive")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<StockItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive(Guid productId, [FromBody] ReceiveStockRequest request,
        CancellationToken ct)
        => ToResponse(await Dispatcher.Send(
            new ReceiveStockCommand(productId, request.Quantity, request.Reference, request.Note), ct));

    /// <summary>Điều chỉnh tồn kho sau kiểm kê.</summary>
    [HttpPost("product/{productId:guid}/adjust")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<StockItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Adjust(Guid productId, [FromBody] AdjustStockRequest request,
        CancellationToken ct)
        => ToResponse(await Dispatcher.Send(
            new AdjustStockCommand(productId, request.NewQuantityOnHand, request.Reason), ct));

    [HttpPatch("product/{productId:guid}/reorder-level")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SetReorderLevel(Guid productId, [FromBody] SetReorderLevelRequest request,
        CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new SetReorderLevelCommand(productId, request.ReorderLevel), ct));
}

public sealed record ReceiveStockRequest(int Quantity, string? Reference, string? Note);
public sealed record AdjustStockRequest(int NewQuantityOnHand, string Reason);
public sealed record SetReorderLevelRequest(int ReorderLevel);
