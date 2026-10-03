using System.Collections.Concurrent;

namespace QrMenu.Infrastructure.Auth;

/// <summary>
/// In-memory brute-force guard: after too many failures for the same key (email) within the window,
/// further attempts are refused until the window passes. Resets when the app restarts.
/// </summary>
public static class LoginAttemptTracker
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Attempts = new();

    public static bool IsLocked(string key)
    {
        if (!Attempts.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (DateTime.UtcNow - entry.WindowStart > Window)
        {
            Attempts.TryRemove(key, out _);
            return false;
        }

        return entry.Count >= MaxFailures;
    }

    public static void RecordFailure(string key)
    {
        var now = DateTime.UtcNow;
        Attempts.AddOrUpdate(
            key,
            _ => (1, now),
            (_, existing) => now - existing.WindowStart > Window ? (1, now) : (existing.Count + 1, existing.WindowStart));
    }

    public static void Reset(string key) => Attempts.TryRemove(key, out _);
}
