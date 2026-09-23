namespace BuildingBlocks.Core.Domain;

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetAtomicValues();

    public bool Equals(ValueObject? other) =>
        other is not null && GetType() == other.GetType() &&
        GetAtomicValues().SequenceEqual(other.GetAtomicValues());

    public override bool Equals(object? obj) => obj is ValueObject vo && Equals(vo);

    public override int GetHashCode() =>
        GetAtomicValues().Aggregate(new HashCode(), (h, v) => { h.Add(v); return h; }).ToHashCode();
}
