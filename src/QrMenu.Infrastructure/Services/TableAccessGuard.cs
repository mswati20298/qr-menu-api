using Microsoft.EntityFrameworkCore;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Persistence;

namespace QrMenu.Infrastructure.Services;

/// <summary>
/// Decides which table a guest order or request belongs to, and whether it may be placed at all.
/// <list type="bullet">
/// <item>A valid table session (from scanning the table's QR) always wins: its table is used, whatever the
/// request says.</item>
/// <item>"Only table QR orders" on: without a valid session a guest cannot order for a table, so a typed or
/// shared link cannot place orders from home.</item>
/// <item>"Takeaway from the link" off: orders without a table are refused (take them at the counter instead).</item>
/// </list>
/// </summary>
public class TableAccessGuard(AppDbContext db, ITableSessionTokens tokens, TimeProvider clock)
{
    public const string QrRequiredCode = "table_qr_required";
    public const string SessionExpiredCode = "table_session_expired";
    public const string TakeawayOffCode = "takeaway_off";

    /// <summary>Returns the table number to use (null = takeaway), or throws with a message for the guest.</summary>
    public async Task<string?> ResolveAsync(Restaurant restaurant, string? tableNumber, string? sessionToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var requested = string.IsNullOrWhiteSpace(tableNumber) ? null : tableNumber.Trim();

        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            var read = tokens.Read(sessionToken);
            var table = read is null
                ? null
                : await db.Tables.AsNoTracking().FirstOrDefaultAsync(
                    t => t.Id == read.Value.TableId && t.RestaurantId == restaurant.Id && t.IsActive, ct);

            if (table is not null && tokens.IsValid(sessionToken, table.Id, table.QrCode, now))
            {
                return table.Number;
            }

            if (restaurant.RequireTableQr)
            {
                throw new ForbiddenException("Please scan the QR code on your table again to order.", SessionExpiredCode);
            }
        }

        if (requested is null)
        {
            if (!restaurant.AllowLinkTakeaway)
            {
                throw new ForbiddenException("Takeaway orders are taken at the counter. Please ask the staff.", TakeawayOffCode);
            }

            return null;
        }

        if (restaurant.RequireTableQr)
        {
            throw new ForbiddenException("Scan the QR code on your table to order.", QrRequiredCode);
        }

        return requested;
    }
}
