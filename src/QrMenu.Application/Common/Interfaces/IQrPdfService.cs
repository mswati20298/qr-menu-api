namespace QrMenu.Application.Common.Interfaces;

public interface IQrPdfService
{
    /// <summary>One card per table. Each entry is the table number and the full address its QR code opens.</summary>
    byte[] GenerateTableQrPdf(string restaurantName, List<(string TableNumber, string Url)> tables);
    byte[] GenerateTableQrPng(string url);
}
