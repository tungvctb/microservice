namespace Order.Domain.Enums;

/// <summary>
/// Vòng đời đơn hàng trong saga:
/// Pending → StockReserved → AwaitingPayment → Paid → Confirmed → Shipped → Completed
/// Bất kỳ bước nào lỗi đều dẫn về Cancelled (kèm bồi hoàn) hoặc Failed.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    StockReserved = 1,
    AwaitingPayment = 2,
    Paid = 3,
    Confirmed = 4,
    Shipped = 5,
    Completed = 6,
    Cancelled = 7,
    Failed = 8
}

public enum PaymentMethodType { CreditCard = 0, BankTransfer = 1, Cod = 2, Wallet = 3 }
