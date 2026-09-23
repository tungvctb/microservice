using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.Application.Common;
using Order.Application.Orders.Commands;
using Order.Application.Orders.Queries;
using Order.Domain.Repositories;

namespace Order.Api.Controllers;

[Route("api/orders")]
[Authorize]
[Produces("application/json")]
public sealed class OrdersController : ApiControllerBase
{
    /// <summary>Danh sách đơn. Khách chỉ thấy đơn của mình, Admin/Manager thấy tất cả.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OrderSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] SearchOrdersQuery query, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(query, ct));

    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<OrderStatistics>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Statistics(CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetOrderStatisticsQuery(), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetOrderByIdQuery(id), ct));

    /// <summary>
    /// Đặt hàng — khởi động saga: định giá qua Catalog (gRPC) → giữ chỗ kho (gRPC)
    /// → ghi đơn + phát OrderPlaced (Kafka) để Payment xử lý tiếp.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Place([FromBody] PlaceOrderCommand command, CancellationToken ct)
    {
        var result = await Dispatcher.Send(command, ct);
        return result.IsSuccess
            ? CreatedResponse(result, nameof(GetById), new { id = result.Value.Id })
            : ToResponse(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new CancelOrderCommand(id, request.Reason), ct));

    [HttpPost("{id:guid}/ship")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Ship(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new ShipOrderCommand(id), ct));

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new CompleteOrderCommand(id), ct));
}

public sealed record CancelOrderRequest(string Reason);
