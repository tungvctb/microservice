using System.Diagnostics;
using BuildingBlocks.Core.Exceptions;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace BuildingBlocks.Infrastructure.Grpc;

/// <summary>Log mọi lời gọi tới và chuyển exception nghiệp vụ thành RpcException có status code đúng nghĩa.</summary>
public sealed class ServerLoggingInterceptor(ILogger<ServerLoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = context.RequestHeaders.GetValue("x-correlation-id") ?? Guid.NewGuid().ToString("N");
        var sw = Stopwatch.StartNew();

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            try
            {
                var response = await continuation(request, context);
                logger.LogInformation("gRPC {Method} OK trong {Elapsed}ms", context.Method, sw.ElapsedMilliseconds);
                return response;
            }
            catch (RpcException)
            {
                throw;
            }
            catch (DomainException ex)
            {
                logger.LogWarning(ex, "gRPC {Method} lỗi nghiệp vụ: {Code}", context.Method, ex.Code);
                throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "gRPC {Method} lỗi hệ thống", context.Method);
                throw new RpcException(new Status(StatusCode.Internal, "Lỗi nội bộ của service."));
            }
        }
    }
}
