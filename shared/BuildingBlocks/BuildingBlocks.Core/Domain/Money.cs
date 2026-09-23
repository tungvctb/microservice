namespace BuildingBlocks.Core.Domain;

/// <summary>Value object tiền tệ. Mọi phép tính tiền trong hệ thống đi qua đây.</summary>
public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public static Money Of(decimal amount, string currency = "VND")
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền không được âm.");
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Thiếu currency.", nameof(currency));
        return new Money(amount, currency.ToUpperInvariant());
    }

    public static Money Zero(string currency = "VND") => new(0m, currency);

    public Money Add(Money other) { EnsureSameCurrency(other); return new Money(Amount + other.Amount, Currency); }
    public Money Multiply(int qty) => new(Amount * qty, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Không thể cộng {Currency} với {other.Currency}.");
    }

    protected override IEnumerable<object?> GetAtomicValues() { yield return Amount; yield return Currency; }

    public override string ToString() => $"{Amount:0.##} {Currency}";
}
