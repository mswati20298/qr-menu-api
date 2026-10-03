using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.MenuScan;

namespace QrMenu.Api.Controllers;

[Route("api/menu-scan")]
public class MenuScanController(IMenuScanService menuScanService) : OwnerControllerBase
{
    private const long MaxFileSizeBytes = 8 * 1024 * 1024;
    private const int MaxImages = 5;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    [HttpPost]
    [RequestSizeLimit(MaxFileSizeBytes * MaxImages)]
    public async Task<ActionResult<MenuScanResultDto>> Scan(List<IFormFile> images, CancellationToken ct)
    {
        if (images is not { Count: > 0 })
        {
            return BadRequest(new { message = "Upload at least one photo of your menu." });
        }

        if (images.Count > MaxImages)
        {
            return BadRequest(new { message = $"You can scan up to {MaxImages} photos at a time." });
        }

        var payload = new List<MenuScanImage>();
        foreach (var file in images)
        {
            if (file.Length == 0)
            {
                continue;
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest(new { message = $"\"{file.FileName}\" exceeds the 8 MB size limit." });
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                return BadRequest(new { message = "Only JPEG, PNG, or WEBP images are allowed." });
            }

            payload.Add(new MenuScanImage(file.OpenReadStream(), file.ContentType));
        }

        var result = await menuScanService.ScanAsync(payload, ct);
        return Ok(result);
    }
}
