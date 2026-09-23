using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Exceptions;

namespace Order.Domain.ValueObjects;

public sealed class ShippingAddress : ValueObject
{
    private ShippingAddress() { }

    public string RecipientName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Street { get; private set; } = string.Empty;
    public string Ward { get; private set; } = string.Empty;
    public string District { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string? Note { get; private set; }

    public static ShippingAddress Create(string recipientName, string phone, string street,
        string ward, string district, string city, string? note = null)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new DomainException("address.recipient_required", "Thiếu tên người nhận.");
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("address.phone_required", "Thiếu số điện thoại.");
        if (string.IsNullOrWhiteSpace(street))
            throw new DomainException("address.street_required", "Thiếu địa chỉ.");

        return new ShippingAddress
        {
            RecipientName = recipientName.Trim(),
            Phone = phone.Trim(),
            Street = street.Trim(),
            Ward = ward.Trim(),
            District = district.Trim(),
            City = city.Trim(),
            Note = note?.Trim()
        };
    }

    public string FullAddress =>
        string.Join(", ", new[] { Street, Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return RecipientName;
        yield return Phone;
        yield return Street;
        yield return Ward;
        yield return District;
        yield return City;
    }
}
