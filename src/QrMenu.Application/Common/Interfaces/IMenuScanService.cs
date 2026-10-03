using QrMenu.Application.MenuScan;

namespace QrMenu.Application.Common.Interfaces;

public interface IMenuScanService
{
    Task<MenuScanResultDto> ScanAsync(List<MenuScanImage> images, CancellationToken ct = default);
}

public record MenuScanImage(Stream Content, string ContentType);
