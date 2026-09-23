using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Results;
using Inventory.Application.Common;
using Inventory.Domain.Errors;
using Inventory.Domain.Repositories;

namespace Inventory.Application.Reservations.Queries;

public sealed record GetReservationByOrderQuery(Guid OrderId) : IQuery<ReservationDto>;

internal sealed class GetReservationByOrderHandler(IReservationRepository reservations)
    : IQueryHandler<GetReservationByOrderQuery, ReservationDto>
{
    public async Task<Result<ReservationDto>> Handle(GetReservationByOrderQuery query, CancellationToken ct)
    {
        var reservation = await reservations.GetByOrderIdAsync(query.OrderId, ct);
        return reservation is null
            ? Result.Failure<ReservationDto>(InventoryErrors.ReservationNotFound(query.OrderId))
            : Result.Success(reservation.ToDto());
    }
}
