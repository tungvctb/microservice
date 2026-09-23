namespace BuildingBlocks.Core.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }
    public DomainException(string code, string message) : base(message) => Code = code;
}

public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base("concurrency.conflict", message) { }
}
