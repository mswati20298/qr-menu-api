using Microsoft.AspNetCore.Hosting;
using QrMenu.Application.Common.Interfaces;

namespace QrMenu.Infrastructure.Files;

public class LocalFileStorageService(IWebHostEnvironment env) : IFileStorageService
{
    private const string UploadsFolder = "uploads";

    public async Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct = default)
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var uploadsPath = Path.Combine(webRoot, UploadsFolder);
        Directory.CreateDirectory(uploadsPath);

        var safeName = $"{Guid.NewGuid()}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(uploadsPath, safeName), content, ct);

        return $"/{UploadsFolder}/{safeName}";
    }
}
