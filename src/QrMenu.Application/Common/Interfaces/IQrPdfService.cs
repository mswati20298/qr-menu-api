namespace QrMenu.Application.Common.Interfaces;

/// <summary>How the cards look: the restaurant's name, tagline, theme colour key and logo (an /uploads/ path).</summary>
public record QrCardBranding(string RestaurantName, string? Tagline, string? ThemeColor, string? LogoUrl);

public interface IQrPdfService
{
    /// <summary>One card per table. Each entry is the table number and the full address its QR code opens.</summary>
    byte[] GenerateTableQrPdf(QrCardBranding branding, List<(string TableNumber, string Url)> tables);
    byte[] GenerateTableQrPng(string url);
}
