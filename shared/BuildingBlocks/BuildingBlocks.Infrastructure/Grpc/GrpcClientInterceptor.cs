using BuildingBlocks.Infrastructure.Web;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Grpc;

/// <summary>Đính correlation id và JWT của request hiện tại vào metadata của lời gọi gRPC đi ra.</summary>
public sealed class CorrelationClientInterceptor(
    IHttpContextAccessor accessor,
    ILogger<CorrelationClientInterceptor> logger) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();

        var httpContext = accessor.HttpContext;
        if (httpContext?.Items[CorrelationMiddleware.HeaderName] is string correlationId &&
            headers.Get(CorrelationMiddleware.HeaderName.ToLowerInvariant()) is null)
        {
            headers.Add(CorrelationMiddleware.HeaderName.ToLowerInvariant(), correlationId);
        }

        var authorization = httpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization) && headers.Get("authorization") is null)
            headers.Add("authorization", authorization);

        logger.LogDebug("gRPC gọi {Method}", context.Method.FullName);

        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, context.Options.WithHeaders(headers));

        return continuation(request, newContext);
    }
}
