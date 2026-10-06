namespace QrMenu.Application.Common;

/// <summary>
/// Restaurants keep Indian hours, whatever time zone the server runs in (the Docker containers run in UTC).
/// </summary>
public static class IndianTime
{
    public static readonly TimeZoneInfo Zone = FindZone();

    /// <summary>A UTC moment as Indian local time.</summary>
    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>
    /// Whether a restaurant with these opening hours (Indian time) is open at the given UTC moment.
    /// Hours that pass midnight (e.g. 6 PM to 2 AM) are handled.
    /// </summary>
    public static bool IsOpen(TimeSpan openTime, TimeSpan closeTime, DateTime utcNow)
    {
        var now = TimeOnly.FromDateTime(FromUtc(utcNow));
        var open = TimeOnly.FromTimeSpan(openTime);
        var close = TimeOnly.FromTimeSpan(closeTime);
        return close > open ? now >= open && now <= close : now >= open || now <= close;
    }

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        // No time zone data on the machine: India has a fixed +5:30 offset and no daylight saving.
        return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "IST", "IST");
    }
}
