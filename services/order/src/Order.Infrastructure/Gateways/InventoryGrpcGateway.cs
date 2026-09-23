using Grpc.Core;
using Microsoft.Extensions.Logging;
using Order.Application.Abstractions;
using Proto = BuildingBlocks.Contracts.Grpc.Inventory;

namespace Order.Infrastructure.Gateways;

public sealed class InventoryGrpcGateway(
    Proto.InventoryGrpcService.InventoryGrpcServiceClient client,
    ILogger<InventoryGrpcGateway> logger) : IInventoryGateway
{
    public async Task<ReserveResult> ReserveAsync(Guid orderId,
        IReadOnlyList<(Guid ProductId, int Quantity)> lines, int ttlSeconds, CancellationToken ct = default)
    {
        var request = new Proto.ReserveStockRequest
        {
            OrderId = orderId.ToString(),
            TtlSeconds = ttlSeconds
        };

        request.Lines.AddRange(lines.Select(l => new Proto.ReserveLine
        {
            ProductId = l.ProductId.ToString(),
            Quantity = l.Quantity
        }));

        try
        {
            var reply = await client.ReserveStockAsync(request, cancellationToken: ct);

            return new ReserveResult(
                reply.Success,
                Guid.TryParse(reply.ReservationId, out var id) ? id : null,
                reply.Message,
                reply.Insufficient.Select(i => new InsufficientLineDto(
                    Guid.Parse(i.ProductId), i.Requested, i.Available)).ToList());
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "gRPC tới Inventory lỗi: {Status}", ex.StatusCode);
            throw new InvalidOperationException($"Inventory không phản hồi ({ex.StatusCode}).", ex);
        }
    }

    public async Task<bool> ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var reply = await client.ReleaseReservationAsync(
                new Proto.ReservationRequest { OrderId = orderId.ToString() }, cancellationToken: ct);
            return reply.Success;
        }
        catch (RpcException ex)
        {
            // Nhả giữ chỗ còn có event OrderCancelled và job hết hạn làm lưới an toàn.
            logger.LogError(ex, "Nhả giữ chỗ cho đơn {OrderId} thất bại — dựa vào cơ chế hết hạn", orderId);
            return false;
        }
    }

    public async Task<bool> CommitAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var reply = await client.CommitReservationAsync(
                new Proto.ReservationRequest { OrderId = orderId.ToString() }, cancellationToken: ct);
            return reply.Success;
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Chốt giữ chỗ cho đơn {OrderId} thất bại", orderId);
            return false;
        }
    }
}
