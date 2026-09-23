using System.Collections.Concurrent;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Core.Results;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Cqrs;

/// <summary>
/// Dispatcher resolve handler qua DI. Wrapper generic được cache nên chỉ reflect 1 lần cho mỗi loại message.
/// </summary>
public sealed class Dispatcher(IServiceProvider provider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var wrapper = (CommandWrapper<TResponse>)Wrappers.GetOrAdd(command.GetType(), static t =>
            Activator.CreateInstance(typeof(CommandWrapperImpl<,>).MakeGenericType(t, typeof(TResponse)))!);
        return wrapper.Handle(command, provider, ct);
    }

    public Task<Result<TResponse>> Ask<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var wrapper = (QueryWrapper<TResponse>)Wrappers.GetOrAdd(query.GetType(), static t =>
            Activator.CreateInstance(typeof(QueryWrapperImpl<,>).MakeGenericType(t, typeof(TResponse)))!);
        return wrapper.Handle(query, provider, ct);
    }

    private abstract class CommandWrapper<TResponse>
    {
        public abstract Task<Result<TResponse>> Handle(object message, IServiceProvider sp, CancellationToken ct);
    }

    private sealed class CommandWrapperImpl<TCommand, TResponse> : CommandWrapper<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public override async Task<Result<TResponse>> Handle(object message, IServiceProvider sp, CancellationToken ct)
        {
            var command = (TCommand)message;

            var validation = await ValidationBehavior.ValidateAsync(command, sp, ct);
            if (validation is not null) return Result.Failure<TResponse>(validation);

            var handler = sp.GetRequiredService<ICommandHandler<TCommand, TResponse>>();
            return await LoggingBehavior.RunAsync(command, sp, () => handler.Handle(command, ct));
        }
    }

    private abstract class QueryWrapper<TResponse>
    {
        public abstract Task<Result<TResponse>> Handle(object message, IServiceProvider sp, CancellationToken ct);
    }

    private sealed class QueryWrapperImpl<TQuery, TResponse> : QueryWrapper<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public override async Task<Result<TResponse>> Handle(object message, IServiceProvider sp, CancellationToken ct)
        {
            var query = (TQuery)message;

            var validation = await ValidationBehavior.ValidateAsync(query, sp, ct);
            if (validation is not null) return Result.Failure<TResponse>(validation);

            var handler = sp.GetRequiredService<IQueryHandler<TQuery, TResponse>>();
            return await LoggingBehavior.RunAsync(query, sp, () => handler.Handle(query, ct));
        }
    }
}
