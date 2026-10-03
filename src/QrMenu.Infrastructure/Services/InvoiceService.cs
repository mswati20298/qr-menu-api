using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Invoices;
using QrMenu.Application.Orders;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

public class InvoiceService(AppDbContext db, IInvoicePdfService pdfService, TimeProvider clock) : IInvoiceService
{
    /// <summary>A table bill picks up unbilled orders from this far back.</summary>
    private static readonly TimeSpan TableWindow = TimeSpan.FromHours(24);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<InvoiceDto> CreateAsync(Guid restaurantId, CreateInvoiceRequest request, CancellationToken ct = default)
    {
        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restaurant not found.");

        List<Order> orders;
        if (request.OrderId.HasValue)
        {
            var order = await db.Orders.Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.RestaurantId == restaurantId, ct)
                ?? throw new NotFoundException("Order not found.");

            if (order.InvoiceId.HasValue)
            {
                return await GetAsync(restaurantId, order.InvoiceId.Value, ct);
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                throw new ConflictException("A cancelled order cannot be billed.");
            }

            orders = [order];
        }
        else
        {
            var table = request.TableNumber!.Trim();
            var since = Now - TableWindow;
            orders = await db.Orders.Include(o => o.Items)
                .Where(o => o.RestaurantId == restaurantId
                    && o.TableNumberSnapshot == table
                    && o.InvoiceId == null
                    && o.Status != OrderStatus.Cancelled
                    && o.CreatedAt >= since)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(ct);

            if (orders.Count == 0)
            {
                throw new ConflictException($"Table {table} has no unbilled orders from the last 24 hours.");
            }
        }

        var first = orders[0];
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            TableNumber = first.TableNumberSnapshot,
            CustomerName = orders.Select(o => o.CustomerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
            CustomerPhone = orders.Select(o => o.CustomerPhone).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
            Subtotal = orders.Sum(o => o.Subtotal),
            ServiceChargeAmount = orders.Sum(o => o.ServiceChargeAmount),
            GstAmount = orders.Sum(o => o.GstAmount),
            Total = orders.Sum(o => o.Total),
            ServiceChargePercentage = restaurant.IsServiceChargeEnabled ? restaurant.ServiceChargePercentage : 0,
            GstPercentage = restaurant.IsGstEnabled ? restaurant.GstPercentage : 0,
            RestaurantName = restaurant.Name,
            RestaurantAddress = restaurant.Address,
            RestaurantPhone = restaurant.Phone,
            GstNumber = restaurant.GstNumber,
            CreatedAt = Now
        };

        foreach (var order in orders)
        {
            order.InvoiceId = invoice.Id;
            order.UpdatedAt = Now;
        }

        db.Invoices.Add(invoice);

        // Next number = highest so far + 1. The unique (RestaurantId, Sequence) index catches the rare
        // case of two invoices being created at the same moment; then simply take the next number.
        for (var attempt = 0; ; attempt++)
        {
            var last = await db.Invoices.Where(i => i.RestaurantId == restaurantId && i.Id != invoice.Id)
                .MaxAsync(i => (int?)i.Sequence, ct) ?? 0;
            invoice.Sequence = last + 1 + attempt;
            invoice.Number = FormatNumber(restaurant.InvoicePrefix, invoice.Sequence);

            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (attempt < 4)
            {
                // Number was taken in the meantime: try the next one.
            }
        }

        invoice.Orders = orders;
        return ToDto(invoice);
    }

    public async Task<InvoiceDto> GetAsync(Guid restaurantId, Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(restaurantId, invoiceId, ct);
        return ToDto(invoice);
    }

    public async Task<InvoicePageDto> ListAsync(Guid restaurantId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Invoices.AsNoTracking().Where(i => i.RestaurantId == restaurantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i => i.Number.Contains(term)
                || (i.TableNumber != null && i.TableNumber == term)
                || (i.CustomerName != null && i.CustomerName.Contains(term))
                || (i.CustomerPhone != null && i.CustomerPhone.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(i => i.Sequence)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id, i.Number, i.CreatedAt, i.TableNumber, i.CustomerName, i.Total,
                Payments = i.Orders.Select(o => o.PaymentStatus).ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new InvoiceSummaryDto(
            r.Id, r.Number, r.CreatedAt, r.TableNumber, r.CustomerName, r.Payments.Count, r.Total, PaymentStatusOf(r.Payments))).ToList();

        return new InvoicePageDto(items, total, page, pageSize);
    }

    public async Task<InvoiceDto> MarkPaidAsync(Guid restaurantId, Guid invoiceId, MarkInvoicePaidRequest request, CancellationToken ct = default)
    {
        var invoice = await LoadAsync(restaurantId, invoiceId, ct, tracking: true);
        var method = Enum.Parse<PaymentMethod>(request.Method, ignoreCase: true);

        foreach (var order in invoice.Orders.Where(o => o.PaymentStatus != OrderPaymentStatus.Paid))
        {
            order.PaymentStatus = OrderPaymentStatus.Paid;
            order.PaymentMethod = method;
            order.PaidAt = Now;
            order.UpdatedAt = Now;
            if (!string.IsNullOrWhiteSpace(request.Reference))
            {
                order.PaymentReference = request.Reference.Trim();
            }
        }

        await db.SaveChangesAsync(ct);
        return ToDto(invoice);
    }

    public async Task<(byte[] Pdf, string Number)> GetPdfAsync(Guid restaurantId, Guid invoiceId, bool receipt, CancellationToken ct = default)
    {
        var invoice = ToDto(await LoadAsync(restaurantId, invoiceId, ct));
        return (pdfService.Generate(invoice, receipt), invoice.Number);
    }

    public static string FormatNumber(string prefix, int sequence) => $"{prefix}-{sequence:D4}";

    private async Task<Invoice> LoadAsync(Guid restaurantId, Guid invoiceId, CancellationToken ct, bool tracking = false)
    {
        var query = db.Invoices.Include(i => i.Orders).ThenInclude(o => o.Items).AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(i => i.Id == invoiceId && i.RestaurantId == restaurantId, ct)
            ?? throw new NotFoundException("Invoice not found.");
    }

    private static InvoiceDto ToDto(Invoice invoice)
    {
        var orders = invoice.Orders.OrderBy(o => o.CreatedAt).ToList();

        // Same item + size + add-ons at the same price → one line with the quantities added up.
        var lines = orders
            .SelectMany(o => o.Items)
            .Select(item =>
            {
                var addOns = string.IsNullOrEmpty(item.AddOnsJson)
                    ? []
                    : JsonSerializer.Deserialize<List<OrderItemAddOnDto>>(item.AddOnsJson) ?? [];
                var addOnNames = addOns.Count > 0 ? string.Join(", ", addOns.Select(a => a.Name)) : null;
                return (item.ItemName, item.VariantName, AddOns: addOnNames, Unit: item.UnitPrice + addOns.Sum(a => a.Price), item.Qty, item.LineTotal);
            })
            .GroupBy(l => (l.ItemName, l.VariantName, l.AddOns, l.Unit))
            .Select(g => new InvoiceLineDto(g.Key.ItemName, g.Key.VariantName, g.Key.AddOns, g.Sum(l => l.Qty), g.Key.Unit, g.Sum(l => l.LineTotal)))
            .ToList();

        return new InvoiceDto(
            invoice.Id, invoice.Number, invoice.CreatedAt, invoice.TableNumber, invoice.CustomerName, invoice.CustomerPhone,
            invoice.RestaurantName, invoice.RestaurantAddress, invoice.RestaurantPhone, invoice.GstNumber,
            lines,
            invoice.Subtotal, invoice.ServiceChargePercentage, invoice.ServiceChargeAmount,
            invoice.GstPercentage, invoice.GstAmount, invoice.Total,
            PaymentStatusOf(orders.Select(o => o.PaymentStatus).ToList()),
            orders.Select(o => o.Id).ToList());
    }

    private static string PaymentStatusOf(List<OrderPaymentStatus> payments)
    {
        if (payments.Count > 0 && payments.All(p => p == OrderPaymentStatus.Paid))
        {
            return "Paid";
        }

        if (payments.Any(p => p == OrderPaymentStatus.Claimed))
        {
            return "Claimed";
        }

        return payments.Any(p => p == OrderPaymentStatus.Paid) ? "PartlyPaid" : "Unpaid";
    }
}
