namespace Order.Application.Abstractions;

/// <summary>
/// Cổng ra tới Catalog. Tầng Application chỉ biết interface này;
/// việc nó là gRPC, REST hay gọi in-memory trong test là chuyện của Infrastructure.
/// </summary>
public interface ICatalogGateway
{
    Task<CatalogValidationResult> ValidateAndPriceAsync(
        IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken ct = default);
}

public sealed record CatalogValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<PricedLineDto> PricedLines);

public sealed record PricedLineDto(
    Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice, string Currency);

/// <summary>Cổng ra tới Inventory cho các bước giữ chỗ / bồi hoàn của saga.</summary>
public interface IInventoryGateway
{
    Task<ReserveResult> ReserveAsync(Guid orderId,
        IReadOnlyList<(Guid ProductId, int Quantity)> lines, int ttlSeconds, CancellationToken ct = default);

    Task<bool> ReleaseAsync(Guid orderId, CancellationToken ct = default);
    Task<bool> CommitAsync(Guid orderId, CancellationToken ct = default);
}

public sealed record ReserveResult(
    bool Success, Guid? ReservationId, string Message,
    IReadOnlyList<InsufficientLineDto> Insufficient);

public sealed record InsufficientLineDto(Guid ProductId, int Requested, int Available);
