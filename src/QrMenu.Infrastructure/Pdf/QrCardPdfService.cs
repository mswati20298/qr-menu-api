using QRCoder;
using QrMenu.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QrMenu.Infrastructure.Pdf;

public class QrCardPdfService : IQrPdfService
{
    static QrCardPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateTableQrPdf(string restaurantName, string slug, List<string> tableNumbers, string baseUrl)
    {
        var cards = tableNumbers.Select(number => (TableNumber: number, QrPng: GeneratePng(BuildUrl(baseUrl, slug, number)))).ToList();

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(15);

                    foreach (var chunk in cards.Chunk(4))
                    {
                        column.Item().Row(row =>
                        {
                            foreach (var card in chunk)
                            {
                                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(15)
                                    .Column(inner =>
                                    {
                                        inner.Spacing(6);
                                        inner.Item().AlignCenter().Text(restaurantName).Bold().FontSize(14);
                                        inner.Item().AlignCenter().Text($"Table {card.TableNumber}").FontSize(12);
                                        inner.Item().AlignCenter().MaxWidth(150).Image(card.QrPng);
                                        inner.Item().AlignCenter().Text("Scan to view menu").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    });
                            }

                            for (var i = chunk.Length; i < 4; i++)
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

    public byte[] GenerateTableQrPng(string slug, string tableNumber, string baseUrl)
    {
        return GeneratePng(BuildUrl(baseUrl, slug, tableNumber));
    }

    private static string BuildUrl(string baseUrl, string slug, string tableNumber) => $"{baseUrl}/m/{slug}?t={tableNumber}";

    private static byte[] GeneratePng(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(20);
    }
}
