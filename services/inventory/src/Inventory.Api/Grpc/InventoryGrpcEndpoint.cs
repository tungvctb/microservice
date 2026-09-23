using BuildingBlocks.Application.Cqrs;
using Grpc.Core;
using Inventory.Application.Reservations.Commands;
using Inventory.Application.Stocks.Queries;
using Proto = BuildingBlocks.Contracts.Grpc.Inventory;

namespace Inventory.Api.Grpc;

/// <summary>
/// Mặt gRPC cho saga đặt hàng: Order service gọi Reserve → Commit (hoặc Release khi bồi hoàn).
/// Dùng gRPC thay vì event ở bước giữ chỗ vì Order cần biết KẾT QUẢ ngay để trả lời khách.
/// </summary>
public sealed class InventoryGrpcEndpoint(IDispatcher dispatcher)
    : Proto.InventoryGrpcService.InventoryGrpcServiceBase
{
    public override async Task<Proto.StockReply> GetStock(Proto.GetStockRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ProductId, out var productId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "productId không hợp lệ."));

        var result = await dispatcher.Ask(new GetStockByProductQuery(productId), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        return ToReply(result.Value);
    }

    public override async Task<Proto.StockListReply> GetStockBatch(Proto.GetStockBatchRequest request,
        ServerCallContext context)
    {
        var ids = request.ProductIds.Select(i => Guid.TryParse(i, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList();

        var result = await dispatcher.Ask(new GetStockBatchQuery(ids), context.CancellationToken);

        var reply = new Proto.StockListReply();
        if (result.IsSuccess) reply.Items.AddRange(result.Value.Select(ToReply));
        return reply;
    }

    public override async Task<Proto.ReserveStockReply> ReserveStock(Proto.ReserveStockRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "orderId không hợp lệ."));

        var lines = request.Lines
            .Select(l => new ReserveLineInput(Guid.Parse(l.ProductId), l.Quantity))
            .ToList();

        var result = await dispatcher.Send(
            new ReserveStockCommand(orderId, lines, request.TtlSeconds), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error.Message));

        var value = result.Value;
        var reply = new Proto.ReserveStockReply
        {
            Success = value.Success,
            ReservationId = value.ReservationId?.ToString() ?? string.Empty,
            Message = value.Message
        };

        reply.Insufficient.AddRange(value.Insufficient.Select(i => new Proto.InsufficientItem
        {
            ProductId = i.ProductId.ToString(),
            Requested = i.Requested,
            Available = i.Available
        }));

        return reply;
    }

    public override async Task<Proto.OperationReply> CommitReservation(Proto.ReservationRequest request,
        ServerCallContext context)
    {
        var orderId = Guid.Parse(request.OrderId);
        var result = await dispatcher.Send(new CommitReservationCommand(orderId), context.CancellationToken);

        return new Proto.OperationReply
        {
            Success = result.IsSuccess,
            Message = result.IsSuccess ? "Đã chốt giữ chỗ." : result.Error.Message
        };
    }

    public override async Task<Proto.OperationReply> ReleaseReservation(Proto.ReservationRequest request,
        ServerCallContext context)
    {
        var orderId = Guid.Parse(request.OrderId);
        var result = await dispatcher.Send(
            new ReleaseReservationCommand(orderId, "Bồi hoàn từ Order service"), context.CancellationToken);

        return new Proto.OperationReply
        {
            Success = result.IsSuccess,
            Message = result.IsSuccess ? "Đã nhả giữ chỗ." : result.Error.Message
        };
    }

    private static Proto.StockReply ToReply(Application.Common.StockItemDto s) => new()
    {
        ProductId = s.ProductId.ToString(),
        Sku = s.Sku,
        QuantityOnHand = s.QuantityOnHand,
        QuantityReserved = s.QuantityReserved,
        QuantityAvailable = s.QuantityAvailable,
        ReorderLevel = s.ReorderLevel,
        IsLowStock = s.IsLowStock,
        WarehouseCode = s.WarehouseCode
    };
}
