namespace QrMenu.Application.Orders;

public record OrderItemAddOnDto(string Name, decimal Price);

public record OrderItemDto(
    Guid Id,
    string ItemName,
    string? VariantName,
    decimal UnitPrice,
    int Qty,
    List<OrderItemAddOnDto> AddOns,
    decimal LineTotal);

public record OrderDto(
    Guid Id,
    string? TableNumber,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    string Status,
    decimal Subtotal,
    decimal ServiceChargeAmount,
    decimal GstAmount,
    decimal Total,
    DateTime CreatedAt,
    List<OrderItemDto> Items,
    string PaymentStatus,
    string? PaymentReference,
    string? PaymentMethod,
    DateTime? PaidAt,
    Guid? InvoiceId,
    string? InvoiceNumber,
    // "Qr" (the guest ordered) or "Staff" (entered from the admin panel).
    string Source = "Qr");

/// <summary>
/// An order entered by staff (walk-in, phone or counter order). TableNumber null = takeaway.
/// SendToKitchen: true puts it on the kitchen screen as New; false records it as already served
/// (for example drinks handed over at the counter).
/// </summary>
public record StaffOrderRequest(
    string? TableNumber,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    bool SkipServiceCharge,
    bool SendToKitchen,
    List<OrderItemInput> Items);

public record OrderItemInput(Guid MenuItemId, Guid? VariantId, List<Guid>? AddOnIds, int Qty);

/// <summary>TableSession = the token from scanning the table's QR; required when the restaurant only takes table-QR orders.</summary>
public record CreateOrderRequest(
    string? TableNumber,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    bool SkipServiceCharge,
    List<OrderItemInput> Items,
    string? TableSession = null);

public record UpdateOrderStatusRequest(string Status);

/// <summary>Customer says they paid by UPI. Reference = the UPI transaction id (UTR), optional.</summary>
public record ClaimPaymentRequest(string? Reference);

/// <summary>
/// Staff: Status "Paid" (Method "Cash", "Upi", "Card" or "Other" required) or "Unpaid" (e.g. a claim that
/// did not arrive in the account).
/// </summary>
public record UpdatePaymentRequest(string Status, string? Method, string? Reference);
