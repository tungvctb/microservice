using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Core.Results;
using Payment.Application.Common;
using Payment.Domain.Enums;
using Payment.Domain.Errors;
using Payment.Domain.Repositories;

namespace Payment.Application.Payments.Queries;

public sealed record GetPaymentByIdQuery(Guid Id) : IQuery<PaymentDto>;

internal sealed class GetPaymentByIdHandler(IPaymentRepository payments)
    : IQueryHandler<GetPaymentByIdQuery, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(GetPaymentByIdQuery query, CancellationToken ct)
    {
        var payment = await payments.GetByIdAsync(query.Id, ct);
        return payment is null
            ? Result.Failure<PaymentDto>(PaymentErrors.NotFound(query.Id))
            : Result.Success(payment.ToDto());
    }
}

public sealed record GetPaymentByOrderQuery(Guid OrderId) : IQuery<PaymentDto>;

internal sealed class GetPaymentByOrderHandler(IPaymentRepository payments)
    : IQueryHandler<GetPaymentByOrderQuery, PaymentDto>
{
    public async Task<Result<PaymentDto>> Handle(GetPaymentByOrderQuery query, CancellationToken ct)
    {
        var payment = await payments.GetByOrderIdAsync(query.OrderId, ct);
        return payment is null
            ? Result.Failure<PaymentDto>(PaymentErrors.NotFoundForOrder(query.OrderId))
            : Result.Success(payment.ToDto());
    }
}

public sealed record SearchPaymentsQuery(
    Guid? OrderId = null, Guid? CustomerId = null, PaymentStatus? Status = null,
    PaymentMethod? Method = null, DateTime? FromUtc = null, DateTime? ToUtc = null,
    int Page = 1, int PageSize = 20) : IQuery<PagedResult<PaymentDto>>;

internal sealed class SearchPaymentsHandler(IPaymentRepository payments)
    : IQueryHandler<SearchPaymentsQuery, PagedResult<PaymentDto>>
{
    public async Task<Result<PagedResult<PaymentDto>>> Handle(SearchPaymentsQuery query, CancellationToken ct)
    {
        var page = await payments.SearchAsync(new PaymentFilter
        {
            OrderId = query.OrderId,
            CustomerId = query.CustomerId,
            Status = query.Status,
            Method = query.Method,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            Page = query.Page,
            PageSize = query.PageSize
        }, ct);

        return Result.Success(new PagedResult<PaymentDto>(
            page.Items.Select(p => p.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount));
    }
}
