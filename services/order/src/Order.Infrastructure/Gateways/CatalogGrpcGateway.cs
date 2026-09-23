using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Order.Application.Abstractions;
using Proto = BuildingBlocks.Contracts.Grpc.Product;

namespace Order.Infrastructure.Gateways;

/// <summary>
/// Adapter gRPC cho Catalog. Giá LUÔN lấy từ Catalog, không bao giờ nhận từ client —
/// nếu không, người dùng có thể sửa payload để mua hàng giá 0đ.
/// </summary>
public sealed class CatalogGrpcGateway(
    Proto.ProductGrpcService.ProductGrpcServiceClient client,
    ILogger<CatalogGrpcGateway> logger) : ICatalogGateway
{
    public async Task<CatalogValidationResult> ValidateAndPriceAsync(
        IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken ct = default)
    {
        var request = new Proto.ValidateProductsRequest();
        request.Lines.AddRange(lines.Select(l => new Proto.ValidateProductLine
        {
            ProductId = l.ProductId.ToString(),
            Quantity = l.Quantity
        }));

        try
        {
            var reply = await client.ValidateProductsAsync(request, cancellationToken: ct);

            return new CatalogValidationResult(
                reply.IsValid,
                reply.Errors.Select(e => e.Message).ToList(),
                reply.PricedLines.Select(p => new PricedLineDto(
                    Guid.Parse(p.ProductId), p.Sku, p.Name, p.Quantity,
                    decimal.Parse(p.UnitPrice.Amount, CultureInfo.InvariantCulture),
                    p.UnitPrice.Currency)).ToList());
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "gRPC tới Catalog lỗi: {Status}", ex.StatusCode);
            throw new InvalidOperationException($"Catalog không phản hồi ({ex.StatusCode}).", ex);
        }
    }
}
