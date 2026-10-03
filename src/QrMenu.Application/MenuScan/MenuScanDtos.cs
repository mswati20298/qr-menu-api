namespace QrMenu.Application.MenuScan;

public record MenuScanItemDto(string Category, string Name, decimal? Price, bool IsVeg, string? Description);

public record MenuScanResultDto(List<MenuScanItemDto> Items);
