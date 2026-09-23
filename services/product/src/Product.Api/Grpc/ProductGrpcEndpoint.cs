using System.Globalization;
using BuildingBlocks.Application.Cqrs;
using Grpc.Core;
using Product.Application.Products.Queries;
using Common = BuildingBlocks.Contracts.Grpc.Common;
using Proto = BuildingBlocks.Contracts.Grpc.Product;

namespace Product.Api.Grpc;

/// <summary>
/// Mặt gRPC của catalog — dành cho service khác gọi nội bộ (Order lấy giá, Inventory đối chiếu SKU).
/// REST dành cho FE, gRPC dành cho service-to-service.
/// </summary>
public sealed class ProductGrpcEndpoint(IDispatcher dispatcher)
    : Proto.ProductGrpcService.ProductGrpcServiceBase
{
    public override async Task<Proto.ProductReply> GetProduct(Proto.GetProductRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id không hợp lệ."));

        var result = await dispatcher.Ask(new GetProductByIdQuery(id), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.NotFound, result.Error.Message));

        return ToReply(result.Value);
    }

    public override async Task<Proto.ProductListReply> GetProductsByIds(Proto.GetProductsByIdsRequest request,
        ServerCallContext context)
    {
        var ids = request.Ids.Select(i => Guid.TryParse(i, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList();

        var result = await dispatcher.Ask(new GetProductsByIdsQuery(ids), context.CancellationToken);

        var reply = new Proto.ProductListReply();
        if (result.IsSuccess) reply.Products.AddRange(result.Value.Select(ToReply));
        return reply;
    }

    /// <summary>Kiểm tra sản phẩm còn bán được không và trả về giá đã chốt cho từng dòng hàng.</summary>
    public override async Task<Proto.ValidateProductsReply> ValidateProducts(Proto.ValidateProductsRequest request,
        ServerCallContext context)
    {
        var ids = request.Lines.Select(l => Guid.TryParse(l.ProductId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct().ToList();

        var result = await dispatcher.Ask(new GetProductsByIdsQuery(ids), context.CancellationToken);
        var reply = new Proto.ValidateProductsReply { IsValid = true };

        if (result.IsFailure)
        {
            reply.IsValid = false;
            reply.Errors.Add(new Common.ErrorDetail { Code = result.Error.Code, Message = result.Error.Message });
            return reply;
        }

        var byId = result.Value.ToDictionary(p => p.Id);

        foreach (var line in request.Lines)
        {
            if (!Guid.TryParse(line.ProductId, out var productId) || !byId.TryGetValue(productId, out var product))
            {
                reply.IsValid = false;
                reply.Errors.Add(new Common.ErrorDetail
                {
                    Code = "product.not_found",
                    Message = $"Không tìm thấy sản phẩm {line.ProductId}."
                });
                continue;
            }

            if (!product.IsActive)
            {
                reply.IsValid = false;
                reply.Errors.Add(new Common.ErrorDetail
                {
                    Code = "product.inactive",
                    Message = $"Sản phẩm {product.Sku} đã ngừng kinh doanh."
                });
                continue;
            }

            reply.PricedLines.Add(new Proto.PricedLine
            {
                ProductId = product.Id.ToString(),
                Sku = product.Sku,
                Name = product.Name,
                Quantity = line.Quantity,
                UnitPrice = ToMoney(product.Price, product.Currency),
                LineTotal = ToMoney(product.Price * line.Quantity, product.Currency)
            });
        }

        return reply;
    }

    private static Proto.ProductReply ToReply(Application.Products.ProductDto p) => new()
    {
        Id = p.Id.ToString(),
        Sku = p.Sku,
        Name = p.Name,
        Description = p.Description ?? string.Empty,
        Price = ToMoney(p.Price, p.Currency),
        CategoryId = p.CategoryId.ToString(),
        CategoryName = p.CategoryName ?? string.Empty,
        IsActive = p.IsActive,
        ImageUrl = p.ImageUrl ?? string.Empty
    };

    private static Common.Money ToMoney(decimal amount, string currency) => new()
    {
        Amount = amount.ToString(CultureInfo.InvariantCulture),
        Currency = currency
    };
}
