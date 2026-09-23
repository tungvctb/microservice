using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Core.Results;
using Order.Application.Common;
using Order.Domain.Enums;
using Order.Domain.Errors;
using Order.Domain.Repositories;

namespace Order.Application.Orders.Queries;

public sealed record GetOrderByIdQuery(Guid Id) : IQuery<OrderDto>;

internal sealed class GetOrderByIdHandler(IOrderRepository orders, ICurrentUser currentUser)
    : IQueryHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(query.Id, ct);
        if (order is null) return Result.Failure<OrderDto>(OrderErrors.NotFound(query.Id));

        // Khách chỉ xem được đơn của chính mình; nhân viên xem được tất cả.
        var isStaff = currentUser.Roles.Any(r => r is "Admin" or "Manager");
        if (!isStaff && order.CustomerId != currentUser.UserId)
            return Result.Failure<OrderDto>(OrderErrors.Forbidden);

        return Result.Success(order.ToDto());
    }
}

public sealed record SearchOrdersQuery(
    Guid? CustomerId = null, OrderStatus? Status = null, string? OrderNumber = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null,
    int Page = 1, int PageSize = 20) : IQuery<PagedResult<OrderSummaryDto>>;

internal sealed class SearchOrdersHandler(IOrderRepository orders, ICurrentUser currentUser)
    : IQueryHandler<SearchOrdersQuery, PagedResult<OrderSummaryDto>>
{
    public async Task<Result<PagedResult<OrderSummaryDto>>> Handle(SearchOrdersQuery query, CancellationToken ct)
    {
        var isStaff = currentUser.Roles.Any(r => r is "Admin" or "Manager");

        // Ép lọc theo chính mình nếu không phải nhân viên — chặn lộ dữ liệu qua query param.
        var customerId = isStaff ? query.CustomerId : currentUser.UserId;

        var page = await orders.SearchAsync(new OrderFilter
        {
            CustomerId = customerId,
            Status = query.Status,
            OrderNumber = query.OrderNumber,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            Page = query.Page,
            PageSize = query.PageSize
        }, ct);

        return Result.Success(new PagedResult<OrderSummaryDto>(
            page.Items.Select(o => o.ToSummary()).ToList(), page.Page, page.PageSize, page.TotalCount));
    }
}

public sealed record GetOrderStatisticsQuery : IQuery<OrderStatistics>;

internal sealed class GetOrderStatisticsHandler(IOrderRepository orders, ICurrentUser currentUser)
    : IQueryHandler<GetOrderStatisticsQuery, OrderStatistics>
{
    public async Task<Result<OrderStatistics>> Handle(GetOrderStatisticsQuery query, CancellationToken ct)
    {
        var isStaff = currentUser.Roles.Any(r => r is "Admin" or "Manager");
        var stats = await orders.GetStatisticsAsync(isStaff ? null : currentUser.UserId, ct);
        return Result.Success(stats);
    }
}
