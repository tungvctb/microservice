using BuildingBlocks.Infrastructure.Web;
using Inventory.Application.Common;
using Inventory.Application.Reservations.Commands;
using Inventory.Application.Reservations.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>
/// Giữ chỗ thường do Order service gọi qua gRPC. REST ở đây phục vụ vận hành/soi lỗi.
/// </summary>
[Route("api/reservations")]
[Produces("application/json")]
public sealed class ReservationsController : ApiControllerBase
{
    [HttpGet("order/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetReservationByOrderQuery(orderId), ct));

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ReserveStockResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reserve([FromBody] ReserveStockCommand command, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(command, ct));

    [HttpPost("order/{orderId:guid}/commit")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Commit(Guid orderId, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new CommitReservationCommand(orderId), ct));

    [HttpPost("order/{orderId:guid}/release")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Release(Guid orderId, [FromBody] ReleaseRequest? request,
        CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new ReleaseReservationCommand(orderId, request?.Reason), ct));
}

public sealed record ReleaseRequest(string? Reason);
