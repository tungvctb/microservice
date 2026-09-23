using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.Common;
using Payment.Application.Payments.Commands;
using Payment.Application.Payments.Queries;

namespace Payment.Api.Controllers;

[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] SearchPaymentsQuery query, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetPaymentByIdQuery(id), ct));

    [HttpGet("order/{orderId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetPaymentByOrderQuery(orderId), ct));

    /// <summary>
    /// Tạo thanh toán thủ công. Luồng chính là tự động: Payment nghe event OrderPlaced từ Kafka.
    /// Endpoint này để thử nghiệm và cho nghiệp vụ thu tiền ngoài luồng.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Process([FromBody] ProcessPaymentCommand command, CancellationToken ct)
    {
        var result = await Dispatcher.Send(command, ct);
        return result.IsSuccess
            ? CreatedResponse(result, nameof(GetById), new { id = result.Value.Id })
            : ToResponse(result);
    }

    [HttpPost("{id:guid}/capture")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Capture(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new CapturePaymentCommand(id), ct));

    [HttpPost("{id:guid}/refund")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new RefundPaymentCommand(id, request.Amount, request.Reason), ct));

    [HttpPost("{id:guid}/fail")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Fail(Guid id, [FromBody] FailRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new FailPaymentCommand(id, request.Reason), ct));
}

public sealed record RefundRequest(decimal Amount, string Reason);
public sealed record FailRequest(string Reason);
