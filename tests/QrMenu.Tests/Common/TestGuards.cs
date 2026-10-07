using Microsoft.Extensions.Options;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Services;

namespace QrMenu.Tests.Common;

public static class TestGuards
{
    public static TableSessionTokens Tokens() => new(Options.Create(new JwtSettings { Secret = "test-secret-key-that-is-at-least-32-characters-long" }));

    public static TableAccessGuard TableAccess(AppDbContext db, TimeProvider? clock = null) => new(db, Tokens(), clock ?? TimeProvider.System);
}
