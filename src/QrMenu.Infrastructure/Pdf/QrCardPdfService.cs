using Microsoft.AspNetCore.Hosting;
using QRCoder;
using QrMenu.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QrMenu.Infrastructure.Pdf;

/// <summary>
/// Printable table cards, six per A4 (cut along the lines). Each card wears the restaurant's theme colour and logo,
/// with a large table number and the QR code in the theme's deep shade (dark enough for every scanner).
/// </summary>
public class QrCardPdfService(IWebHostEnvironment env) : IQrPdfService
{
    // Same palettes as the app (styles/_theme.scss): main colour, deep colour, soft tint.
    private static readonly Dictionary<string, (string Main, string Deep, string Soft)> Palettes = new()
    {
        ["masala"] = ("#C2410C", "#7C2D12", "#FFF1E8"),
        ["ocean"] = ("#2563EB", "#1E3A8A", "#EAF1FF"),
        ["purple"] = ("#7C3AED", "#4C1D95", "#F3EDFF"),
        ["emerald"] = ("#059669", "#064E3B", "#E7F8F1"),
        ["sunset"] = ("#EA580C", "#7C2D12", "#FFF0E6"),
        ["ruby"] = ("#DC2626", "#7F1D1D", "#FDECEC"),
        ["rose"] = ("#E11D48", "#881337", "#FDEBF0"),
        ["cyan"] = ("#0891B2", "#164E63", "#E6F7FB"),
        ["amber"] = ("#D97706", "#78350F", "#FFF6E5")
    };

    static QrCardPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateTableQrPdf(QrCardBranding branding, List<(string TableNumber, string Url)> tables)
    {
        var palette = Palettes.GetValueOrDefault(branding.ThemeColor?.ToLowerInvariant() ?? "", Palettes["masala"]);
        var deep = HexToRgba(palette.Deep);
        var cards = tables.Select(t => (t.TableNumber, QrPng: GeneratePng(t.Url, deep))).ToList();
        var logo = ReadUpload(branding.LogoUrl);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(22);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#1F1A14"));

                page.Content().Column(column =>
                {
                    column.Spacing(12);
                    foreach (var pair in cards.Chunk(2))
                    {
                        column.Item().Height(252).Row(row =>
                        {
                            row.Spacing(12);
                            foreach (var card in pair)
                            {
                                row.RelativeItem().Element(c => Card(c, branding, palette, logo, card.TableNumber, card.QrPng));
                            }
                            if (pair.Length == 1)
                            {
                                row.RelativeItem();
                            }
                        });
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void Card(IContainer container, QrCardBranding branding, (string Main, string Deep, string Soft) palette,
        byte[]? logo, string tableNumber, byte[] qrPng)
    {
        container.Border(1).BorderColor("#D8D2CA").Column(card =>
        {
            // Header band: logo + name in the theme colour.
            card.Item().Background(palette.Main).PaddingVertical(9).PaddingHorizontal(12).Row(header =>
            {
                header.Spacing(8);
                if (logo is not null)
                {
                    header.ConstantItem(30).Height(30).Background(Colors.White).Padding(2).AlignCenter().AlignMiddle().Image(logo).FitArea();
                }
                header.RelativeItem().AlignMiddle().Column(name =>
                {
                    name.Item().Text(branding.RestaurantName).FontSize(13).Bold().FontColor(Colors.White).ClampLines(1);
                    if (!string.IsNullOrWhiteSpace(branding.Tagline))
                    {
                        name.Item().Text(branding.Tagline).FontSize(7.5f).FontColor("#FFFFFFCC").ClampLines(1);
                    }
                });
            });

            card.Item().Background(palette.Soft).PaddingTop(8).PaddingBottom(6).PaddingHorizontal(12).Row(body =>
            {
                // Table number, big enough to read from the next table.
                body.RelativeItem().PaddingRight(8).AlignMiddle().Column(left =>
                {
                    left.Item().Text("TABLE").FontSize(8).Bold().LetterSpacing(0.15f).FontColor(palette.Deep);
                    left.Item().Text(tableNumber).FontSize(34).Bold().FontColor(palette.Deep).ClampLines(1);
                    left.Item().PaddingTop(6).Text("Scan to see the menu & order from your table")
                        .FontSize(8.5f).FontColor("#4B4038");
                    left.Item().PaddingTop(8).Column(steps =>
                    {
                        steps.Spacing(3);
                        Step(steps, "1", "Scan the QR", palette.Main);
                        Step(steps, "2", "Pick your dishes", palette.Main);
                        Step(steps, "3", "Order: it reaches the kitchen", palette.Main);
                    });
                });

                // Fixed size: 134 pt QR + 6 pt white margin + 1 pt border on each side = 148 pt.
                body.ConstantItem(148).AlignMiddle()
                    .Background(Colors.White).Border(1).BorderColor("#E8DFD2").Padding(6)
                    .Height(134).Image(qrPng).FitArea();
            });

            // Fills the rest of the card; the credit sits at its bottom (a separate item after Extend would not fit).
            card.Item().Extend().Background(palette.Soft).AlignBottom().PaddingBottom(7).AlignCenter()
                .Text("Powered by QRenvo").FontSize(6.5f).FontColor("#8A7F74");
        });
    }

    private static void Step(ColumnDescriptor steps, string number, string text, string colour)
    {
        steps.Item().Row(row =>
        {
            row.Spacing(5);
            row.ConstantItem(13).Height(13).Background(colour).AlignCenter().AlignMiddle()
                .Text(number).FontSize(7).Bold().FontColor(Colors.White);
            row.RelativeItem().AlignMiddle().Text(text).FontSize(8).FontColor("#3B322B");
        });
    }

    public byte[] GenerateTableQrPng(string url) => GeneratePng(url, [0, 0, 0, 255]);

    private static byte[] GeneratePng(string url, byte[] darkRgba)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(20, darkRgba, [255, 255, 255, 255]);
    }

    /// <summary>The logo file from our uploads folder; null when there is none or it cannot be read.</summary>
    private byte[]? ReadUpload(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return null;
        }
        var name = Path.GetFileName(url);
        var path = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", name);
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static byte[] HexToRgba(string hex) =>
    [
        Convert.ToByte(hex.Substring(1, 2), 16),
        Convert.ToByte(hex.Substring(3, 2), 16),
        Convert.ToByte(hex.Substring(5, 2), 16),
        255
    ];
}
