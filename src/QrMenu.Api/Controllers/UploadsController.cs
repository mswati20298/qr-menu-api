using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Common.Interfaces;

namespace QrMenu.Api.Controllers;

[Route("api/uploads")]
public class UploadsController(IFileStorageService fileStorageService, IImageOptimizer imageOptimizer) : OwnerControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    // Phone camera photos are often 5–12 MB; they are scaled down before saving, so accept up to 15 MB in.
    private const long MaxFileSizeBytes = 15 * 1024 * 1024;

    /// <summary>purpose: "item" (default), "background" or "logo"; decides how large the saved image is.</summary>
    [HttpPost("image")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string? purpose, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "No file provided." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File exceeds the 15 MB size limit." });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Only JPEG, PNG, or WEBP images are allowed." });
        }

        var imagePurpose = purpose?.ToLowerInvariant() switch
        {
            "background" => ImagePurpose.Background,
            "logo" => ImagePurpose.Logo,
            _ => ImagePurpose.Item
        };

        // Check, rotate, strip hidden data and shrink before anything is stored.
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;
        var image = await imageOptimizer.OptimizeAsync(stream, imagePurpose, ct);

        var url = await fileStorageService.SaveAsync(image.Content, image.Extension, ct);
        return Ok(new { url, width = image.Width, height = image.Height, bytes = image.Content.Length });
    }
}
