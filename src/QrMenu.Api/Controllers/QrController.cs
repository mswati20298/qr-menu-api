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
        var activeTables = tables.Where(t => t.IsActive).ToList();

        if (activeTables.Count == 0)
        {
            return BadRequest(new { message = "Add at least one table under Table Management before generating QR cards." });
        }

        var cards = activeTables
            .Select(t => (t.Number, MenuLinks.TableUrl(_site, restaurant.Slug, restaurant.Subdomain, t.Number, t.QrCode)))
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

        var table = (await tableService.GetAllAsync(RestaurantId, ct)).FirstOrDefault(t => t.Number == tableNumber)
            ?? throw new NotFoundException("Table not found.");
        var pngBytes = qrPdfService.GenerateTableQrPng(
            MenuLinks.TableUrl(_site, restaurant.Slug, restaurant.Subdomain, table.Number, table.QrCode));

        return File(pngBytes, "image/png");
    }
}
