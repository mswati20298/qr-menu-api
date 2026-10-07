namespace QrMenu.Application.PublicMenu;

public interface IPublicMenuService
{
    Task<PublicMenuResponse> GetMenuAsync(string slug, CancellationToken ct = default);
    Task LogScanAsync(string slug, string? table, CancellationToken ct = default);
    /// <summary>Checks the code from a table's QR and returns a time-limited table session for this phone.</summary>
    Task<TableSessionDto> StartTableSessionAsync(string slug, StartTableSessionRequest request, CancellationToken ct = default);
}
