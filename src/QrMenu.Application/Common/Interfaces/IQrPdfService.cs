namespace QrMenu.Application.Common.Interfaces;

public interface IQrPdfService
{
    byte[] GenerateTableQrPdf(string restaurantName, string slug, List<string> tableNumbers, string baseUrl);
    byte[] GenerateTableQrPng(string slug, string tableNumber, string baseUrl);
}
