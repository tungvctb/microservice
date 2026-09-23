using System.Diagnostics;
using BuildingBlocks.Core.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

public static class LoggingBehavior
{
    public static async Task<Result<TResponse>> RunAsync<TMessage, TResponse>(
        TMessage message, IServiceProvider sp, Func<Task<Result<TResponse>>> next)
    {
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Cqrs");
        var name = typeof(TMessage).Name;
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await next();
            sw.Stop();

            if (result.IsFailure)
                logger?.LogWarning("{Message} thất bại sau {Elapsed}ms: {Code} - {Detail}",
                    name, sw.ElapsedMilliseconds, result.Error.Code, result.Error.Message);
            else
                logger?.LogInformation("{Message} xử lý xong sau {Elapsed}ms", name, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger?.LogError(ex, "{Message} ném exception sau {Elapsed}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
