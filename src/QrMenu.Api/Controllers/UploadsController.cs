using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Common.Interfaces;

namespace QrMenu.Api.Controllers;

[Route("api/uploads")]
public class UploadsController(IFileStorageService fileStorageService) : OwnerControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "No file provided." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File exceeds the 5 MB size limit." });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Only JPEG, PNG, or WEBP images are allowed." });
        }

        await using var stream = file.OpenReadStream();
        var url = await fileStorageService.SaveAsync(stream, file.FileName, file.ContentType, ct);

        return Ok(new { url });
    }
}
