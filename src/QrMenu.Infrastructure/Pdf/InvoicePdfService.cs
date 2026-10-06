using System.Globalization;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Invoices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QrMenu.Infrastructure.Pdf;

/// <summary>
/// Invoice as an 80 mm thermal-printer receipt (default) or an A4 page. Amounts are printed as "Rs."
/// because not every server font has the ₹ glyph.
/// </summary>
public class InvoicePdfService : IInvoicePdfService
{
    // Indian number format; dates are converted to Indian time with IndianTime, whatever the server's zone.
    private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");

    static InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(InvoiceDto invoice, bool receipt)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                if (receipt)
                {
                    page.ContinuousSize(80, Unit.Millimetre);
                    page.Margin(4, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(8));
                }
                else
                {
                    page.Size(PageSizes.A4);
                    page.Margin(16, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));
                }

                page.Content().Column(col =>
                {
                    col.Spacing(receipt ? 3 : 6);
                    Header(col, invoice, receipt);
                    Divider(col);
                    Meta(col, invoice);
                    Divider(col);
                    Lines(col, invoice, receipt);
                    Divider(col);
                    Totals(col, invoice, receipt);
                    Divider(col);
                    col.Item().AlignCenter().Text(PaymentLine(invoice)).Bold();
                    col.Item().PaddingTop(4).AlignCenter().Text("Thank you! Visit again.").Italic();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void Header(ColumnDescriptor col, InvoiceDto invoice, bool receipt)
    {
        col.Item().AlignCenter().Text(invoice.RestaurantName).Bold().FontSize(receipt ? 12 : 18);
        if (!string.IsNullOrWhiteSpace(invoice.RestaurantAddress))
        {
            col.Item().AlignCenter().Text(invoice.RestaurantAddress).FontColor(Colors.Grey.Darken2);
        }
        if (!string.IsNullOrWhiteSpace(invoice.RestaurantPhone))
        {
            col.Item().AlignCenter().Text($"Phone: {invoice.RestaurantPhone}");
        }
        if (!string.IsNullOrWhiteSpace(invoice.GstNumber))
        {
            col.Item().AlignCenter().Text($"GSTIN: {invoice.GstNumber}").SemiBold();
        }
        col.Item().PaddingTop(2).AlignCenter().Text(invoice.GstNumber is null ? "BILL" : "TAX INVOICE").Bold().FontSize(receipt ? 10 : 13);
    }

    private static void Meta(ColumnDescriptor col, InvoiceDto invoice)
    {
        var issued = IndianTime.FromUtc(invoice.CreatedAt);

        col.Item().Row(row =>
        {
            row.RelativeItem().Text($"Invoice: {invoice.Number}").SemiBold();
            row.RelativeItem().AlignRight().Text(issued.ToString("dd MMM yyyy, hh:mm tt", IndianCulture));
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(invoice.TableNumber is null ? "Takeaway" : $"Table {invoice.TableNumber}");
            if (!string.IsNullOrWhiteSpace(invoice.CustomerName))
            {
                row.RelativeItem().AlignRight().Text(invoice.CustomerName);
            }
        });
    }

    private static void Lines(ColumnDescriptor col, InvoiceDto invoice, bool receipt)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(receipt ? 5 : 6);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(2.2f);
                c.RelativeColumn(2.4f);
            });

            table.Header(h =>
            {
                h.Cell().Text("Item").Bold();
                h.Cell().AlignRight().Text("Qty").Bold();
                h.Cell().AlignRight().Text("Rate").Bold();
                h.Cell().AlignRight().Text("Amount").Bold();
            });

            foreach (var line in invoice.Lines)
            {
                table.Cell().PaddingTop(2).Column(c =>
                {
                    c.Item().Text(line.Variant is null ? line.Name : $"{line.Name} ({line.Variant})");
                    if (line.AddOns is not null)
                    {
                        c.Item().Text($"+ {line.AddOns}").FontSize(receipt ? 7 : 8).FontColor(Colors.Grey.Darken2);
                    }
                });
                table.Cell().PaddingTop(2).AlignRight().Text(line.Qty.ToString(IndianCulture));
                table.Cell().PaddingTop(2).AlignRight().Text(Money(line.UnitPrice));
                table.Cell().PaddingTop(2).AlignRight().Text(Money(line.Amount));
            }
        });
    }

    private static void Totals(ColumnDescriptor col, InvoiceDto invoice, bool receipt)
    {
        void Line(string label, decimal amount, bool bold = false)
        {
            col.Item().Row(row =>
            {
                var left = row.RelativeItem().Text(label);
                var right = row.ConstantItem(receipt ? 70 : 110).AlignRight().Text($"Rs. {Money(amount)}");
                if (bold)
                {
                    left.Bold().FontSize(receipt ? 10 : 12);
                    right.Bold().FontSize(receipt ? 10 : 12);
                }
            });
        }

        Line("Subtotal", invoice.Subtotal);
        if (invoice.ServiceChargeAmount > 0)
        {
            Line($"Service charge ({Percent(invoice.ServiceChargePercentage)})", invoice.ServiceChargeAmount);
        }

        if (invoice.GstAmount > 0)
        {
            // Registered restaurant: show the intra-state split (half CGST, half SGST).
            if (invoice.GstNumber is not null)
            {
                var cgst = Math.Round(invoice.GstAmount / 2, 2);
                var half = Percent(invoice.GstPercentage / 2);
                Line($"CGST ({half})", cgst);
                Line($"SGST ({half})", invoice.GstAmount - cgst);
            }
            else
            {
                Line($"GST ({Percent(invoice.GstPercentage)})", invoice.GstAmount);
            }
        }

        Line("Total", invoice.Total, bold: true);
    }

    private static void Divider(ColumnDescriptor col) =>
        col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

    private static string PaymentLine(InvoiceDto invoice) => invoice.PaymentStatus switch
    {
        "Paid" => "PAID",
        "PartlyPaid" => "PARTLY PAID",
        _ => "AMOUNT DUE"
    };

    private static string Money(decimal amount) => amount.ToString("#,##0.00", IndianCulture);

    private static string Percent(decimal value) => $"{value.ToString("0.##", IndianCulture)}%";
}
