using Microsoft.AspNetCore.Hosting;
using QrMenu.Application.Common.Interfaces;

namespace QrMenu.Infrastructure.Files;

public class LocalFileStorageService(IWebHostEnvironment env) : IFileStorageService
{
    private const string UploadsFolder = "uploads";

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var uploadsPath = Path.Combine(webRoot, UploadsFolder);
        Directory.CreateDirectory(uploadsPath);

        var extension = Path.GetExtension(fileName);
        var safeName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsPath, safeName);

        await using var fileStream = new FileStream(fullPath, FileMode.Create);
        await content.CopyToAsync(fileStream, ct);

        return $"/{UploadsFolder}/{safeName}";
    }
}
