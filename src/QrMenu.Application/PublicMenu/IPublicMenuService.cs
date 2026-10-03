namespace QrMenu.Application.PublicMenu;

public interface IPublicMenuService
{
    Task<PublicMenuResponse> GetMenuAsync(string slug, CancellationToken ct = default);
    Task LogScanAsync(string slug, string? table, CancellationToken ct = default);
}
