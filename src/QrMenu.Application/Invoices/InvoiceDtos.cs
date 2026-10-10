namespace QrMenu.Application.Invoices;

/// <summary>Same item ordered more than once (with the same size and add-ons) is shown as one line.</summary>
public record InvoiceLineDto(string Name, string? Variant, string? AddOns, int Qty, decimal UnitPrice, decimal Amount);

/// <summary>PaymentStatus: "Paid" (every order paid), "Claimed", "PartlyPaid" or "Unpaid".</summary>
public record InvoiceDto(
    Guid Id,
    string Number,
    DateTime CreatedAt,
    string? TableNumber,
    string? CustomerName,
    string? CustomerPhone,
    string RestaurantName,
    string? RestaurantAddress,
    string? RestaurantPhone,
    string? GstNumber,
    List<InvoiceLineDto> Lines,
    decimal Subtotal,
    decimal ServiceChargePercentage,
    decimal ServiceChargeAmount,
    decimal GstPercentage,
    decimal GstAmount,
    decimal Total,
    string PaymentStatus,
    List<Guid> OrderIds);

public record InvoiceSummaryDto(
    Guid Id,
    string Number,
    DateTime CreatedAt,
    string? TableNumber,
    string? CustomerName,
    int OrdersCount,
    decimal Total,
    string PaymentStatus);

/// <summary>One bill as a row of the GST / sales report.</summary>
public record InvoiceExportRowDto(
    string Number,
    DateTime CreatedAt,
    string? TableNumber,
    string? CustomerName,
    string? CustomerPhone,
    int OrdersCount,
    decimal Subtotal,
    decimal ServiceChargeAmount,
    decimal GstPercentage,
    decimal GstAmount,
    decimal Total,
    string PaymentStatus,
    string? PaymentMethods);

public record InvoicePageDto(List<InvoiceSummaryDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// Give OrderId to bill one order, or TableNumber to bill every order at that table that is not cancelled,
/// not billed yet and was placed in the last 24 hours.
/// </summary>
public record CreateInvoiceRequest(Guid? OrderId, string? TableNumber);

/// <summary>Method: "Cash", "Upi", "Card" or "Other". Marks every order on the invoice as paid.</summary>
public record MarkInvoicePaidRequest(string Method, string? Reference);

/// <summary>
/// Counter bill: staff picks dishes from the menu and gets an invoice straight away.
/// PaidWith "Cash", "Upi", "Card" or "Other" marks it paid; null leaves it unpaid.
/// </summary>
public record ManualInvoiceRequest(
    string? TableNumber,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    bool SkipServiceCharge,
    bool SendToKitchen,
    List<QrMenu.Application.Orders.OrderItemInput> Items,
    string? PaidWith);
