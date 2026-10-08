using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Common.Interfaces;

namespace QrMenu.Api.Controllers;

/// <summary>
/// Small copies of uploaded photos for lists: /uploads/w320/{file}. The full photo (up to 1200 px) is ten times
/// the bytes a 104 px menu thumbnail needs. Each size is made once, kept next to the uploads and then cached
/// by browsers and Cloudflare. Works for every photo already uploaded, nothing has to be re-uploaded.
/// </summary>
[ApiController]
[Route("uploads")]
public partial class ThumbnailsController(IWebHostEnvironment env, IImageOptimizer optimizer, ILogger<ThumbnailsController> logger)
    : ControllerBase
{
    // Only these widths, so nobody can make the server render endless sizes.
    private static readonly HashSet<int> Widths = [160, 320, 640];

    // Uploads are stored under random GUID names; anything else (../, other folders) is refused.
    [GeneratedRegex(@"^[0-9a-fA-F-]{36}\.(jpg|jpeg|png|webp)$")]
    private static partial Regex UploadName();

    private static readonly SemaphoreSlim Gate = new(2, 2);

    [HttpGet("w{width:int}/{file}")]
    [HttpHead("w{width:int}/{file}")]
    public async Task<IActionResult> Get(int width, string file, CancellationToken ct)
    {
        if (!Widths.Contains(width) || !UploadName().IsMatch(file))
        {
            return NotFound();
        }

        var uploads = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
        var original = Path.Combine(uploads, file);
        if (!System.IO.File.Exists(original))
        {
            return NotFound();
        }

        var folder = Path.Combine(uploads, $"_w{width}");
        var cached = Cached(folder, file);
        if (cached is null)
        {
            await Gate.WaitAsync(ct);
            try
            {
                cached = Cached(folder, file);
                if (cached is null)
                {
                    await using var source = System.IO.File.OpenRead(original);
                    var thumb = await optimizer.ThumbnailAsync(source, width, ct);
                    Directory.CreateDirectory(folder);
                    cached = Path.Combine(folder, file + thumb.Extension);
                    await System.IO.File.WriteAllBytesAsync(cached, thumb.Content, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A file the optimizer cannot read: fall back to the full photo rather than a broken image.
                logger.LogWarning(ex, "Could not make a {Width}px thumbnail of {File}", width, file);
                return Redirect($"/uploads/{file}");
            }
            finally
            {
                Gate.Release();
            }
        }

        Response.Headers.CacheControl = "public, max-age=2592000";
        return PhysicalFile(cached, cached.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/jpeg");
    }

    private static string? Cached(string folder, string file) =>
        new[] { ".jpg", ".png" }.Select(ext => Path.Combine(folder, file + ext)).FirstOrDefault(System.IO.File.Exists);
}
