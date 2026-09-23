namespace BuildingBlocks.Application.Cqrs;

/// <summary>Placeholder cho command không trả dữ liệu.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}

public interface ICommand<TResponse>;
public interface ICommand : ICommand<Unit>;
public interface IQuery<TResponse>;
