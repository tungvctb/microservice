using BuildingBlocks.Infrastructure.Grpc;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Grpc;

public static class GrpcClientRegistration
{
    /// <summary>
    /// Đăng ký gRPC client kèm interceptor propagate correlation và policy retry/timeout.
    /// Địa chỉ đọc từ cấu hình "GrpcClients:{name}".
    /// </summary>
    public static IHttpClientBuilder AddGrpcServiceClient<TClient>(
        this IServiceCollection services, IConfiguration configuration, string name)
        where TClient : class
    {
        var address = configuration[$"GrpcClients:{name}"]
                      ?? throw new InvalidOperationException($"Thiếu cấu hình GrpcClients:{name}");

        return services.AddGrpcClient<TClient>(o => o.Address = new Uri(address))
            .AddInterceptor<CorrelationClientInterceptor>(InterceptorScope.Client)
            .ConfigureChannel(channel =>
            {
                channel.MaxReceiveMessageSize = 8 * 1024 * 1024;
                channel.ServiceConfig = new global::Grpc.Net.Client.Configuration.ServiceConfig
                {
                    MethodConfigs =
                    {
                        new global::Grpc.Net.Client.Configuration.MethodConfig
                        {
                            Names = { global::Grpc.Net.Client.Configuration.MethodName.Default },
                            RetryPolicy = new global::Grpc.Net.Client.Configuration.RetryPolicy
                            {
                                MaxAttempts = 3,
                                InitialBackoff = TimeSpan.FromMilliseconds(200),
                                MaxBackoff = TimeSpan.FromSeconds(2),
                                BackoffMultiplier = 2,
                                RetryableStatusCodes = { global::Grpc.Core.StatusCode.Unavailable }
                            }
                        }
                    }
                };
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
            });
    }
}
