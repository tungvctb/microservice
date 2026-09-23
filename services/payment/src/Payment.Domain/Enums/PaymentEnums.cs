namespace Payment.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,   // đã giữ tiền, chưa trừ
    Captured = 2,     // đã trừ tiền
    Failed = 3,
    Refunded = 4,
    PartiallyRefunded = 5
}

public enum PaymentMethod { CreditCard = 0, BankTransfer = 1, Cod = 2, Wallet = 3 }
