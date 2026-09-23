using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Gán/propagate correlation id cho mỗi request. Id này đi theo HTTP header, gRPC metadata
/// và header Kafka, nhờ đó trace được 1 giao dịch xuyên suốt nhiều service.
/// </summary>
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming) &&
                            !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
