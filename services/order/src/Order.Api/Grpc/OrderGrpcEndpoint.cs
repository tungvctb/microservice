using System.Globalization;
using BuildingBlocks.Application.Cqrs;
using Grpc.Core;
using Order.Application.Orders.Queries;
using Common = BuildingBlocks.Contracts.Grpc.Common;
using Proto = BuildingBlocks.Contracts.Grpc.Order;

namespace Order.Api.Grpc;

public sealed class OrderGrpcEndpoint(IDispatcher dispatcher) : Proto.OrderGrpcService.OrderGrpcServiceBase
{
    public override async Task<Proto.OrderReply> GetOrder(Proto.GetOrderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id không hợp lệ."));

        var result = await dispatcher.Ask(new GetOrderByIdQuery(id), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        return ToReply(result.Value);
    }

    public override async Task<Proto.OrderListReply> GetOrdersByCustomer(
        Proto.GetOrdersByCustomerRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CustomerId, out var customerId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "customerId không hợp lệ."));

        var page = request.Page?.Page ?? 1;
        var pageSize = request.Page?.PageSize ?? 20;

        var result = await dispatcher.Ask(
            new SearchOrdersQuery(CustomerId: customerId, Page: page, PageSize: pageSize),
            context.CancellationToken);

        var reply = new Proto.OrderListReply
        {
            Page = new Common.PageInfo { Page = page, PageSize = pageSize, TotalCount = 0 }
        };

        if (result.IsFailure) return reply;

        reply.Page.TotalCount = result.Value.TotalCount;
        reply.Orders.AddRange(result.Value.Items.Select(o => new Proto.OrderReply
        {
            Id = o.Id.ToString(),
            OrderNumber = o.OrderNumber,
            CustomerId = customerId.ToString(),
            Status = o.Status.ToString(),
            Total = Money(o.TotalAmount, o.Currency),
            CreatedAt = o.CreatedAtUtc.ToString("O")
        }));

        return reply;
    }

    private static Proto.OrderReply ToReply(Application.Common.OrderDto o)
    {
        var reply = new Proto.OrderReply
        {
            Id = o.Id.ToString(),
            OrderNumber = o.OrderNumber,
            CustomerId = o.CustomerId.ToString(),
            Status = o.Status.ToString(),
            Total = Money(o.TotalAmount, o.Currency),
            CreatedAt = o.CreatedAtUtc.ToString("O")
        };

        reply.Lines.AddRange(o.Lines.Select(l => new Proto.OrderLineReply
        {
            ProductId = l.ProductId.ToString(),
            Sku = l.Sku,
            Name = l.ProductName,
            Quantity = l.Quantity,
            UnitPrice = Money(l.UnitPrice, l.Currency)
        }));

        return reply;
    }

    private static Common.Money Money(decimal amount, string currency) => new()
    {
        Amount = amount.ToString(CultureInfo.InvariantCulture),
        Currency = currency
    };
}
