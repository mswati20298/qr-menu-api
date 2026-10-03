namespace QrMenu.Domain.Entities;

public enum PaymentMethod
{
    Cash,
    Upi,
    BankTransfer,
    Card,
    Other,

    /// <summary>Paid by the owner through the online payment gateway (Razorpay).</summary>
    Online
}
