using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Restaurants;
using QrMenu.Application.Tables;

namespace QrMenu.Api.Controllers;

[Route("api/qr")]
public class QrController(
    IQrPdfService qrPdfService,
    IRestaurantService restaurantService,
    ITableService tableService,
    IOptions<SiteSettings> siteOptions) : OwnerControllerBase
{
    private readonly SiteSettings _site = siteOptions.Value;

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetQrPdf(string slug, CancellationToken ct)
    {
        var restaurant = await restaurantService.GetAsync(RestaurantId, ct);
        if (restaurant.Slug != slug)
        {
            throw new NotFoundException("Restaurant not found.");
        }

        var tables = await tableService.GetAllAsync(RestaurantId, ct);
        var tableNumbers = tables.Where(t => t.IsActive).Select(t => t.Number).ToList();

        if (tableNumbers.Count == 0)
        {
            return BadRequest(new { message = "Add at least one table under Table Management before generating QR cards." });
        }

        var cards = tableNumbers
            .Select(n => (n, MenuLinks.TableUrl(_site, restaurant.Slug, restaurant.Subdomain, n)))
            .ToList();
        var pdfBytes = qrPdfService.GenerateTableQrPdf(restaurant.Name, cards);

        return File(pdfBytes, "application/pdf", $"{restaurant.Slug}-qr-cards.pdf");
    }

    [HttpGet("{slug}/table/{tableNumber}")]
    public async Task<IActionResult> GetTableQrPng(string slug, string tableNumber, CancellationToken ct)
    {
        var restaurant = await restaurantService.GetAsync(RestaurantId, ct);
        if (restaurant.Slug != slug)
        {
            throw new NotFoundException("Restaurant not found.");
        }

        var pngBytes = qrPdfService.GenerateTableQrPng(
            MenuLinks.TableUrl(_site, restaurant.Slug, restaurant.Subdomain, tableNumber));

        return File(pngBytes, "image/png");
    }
}
