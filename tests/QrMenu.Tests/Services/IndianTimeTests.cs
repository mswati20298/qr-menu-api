using FluentAssertions;
using QrMenu.Application.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class IndianTimeTests
{
    private static readonly TimeSpan TenAm = new(10, 0, 0);
    private static readonly TimeSpan ElevenPm = new(23, 0, 0);

    [Fact]
    public void IsOpen_UsesIndianTime_NotServerUtc()
    {
        // 08:12 UTC = 13:42 in India: open, although 08:12 is before the 10 AM opening.
        var utc = new DateTime(2026, 10, 6, 8, 12, 0, DateTimeKind.Utc);

        IndianTime.IsOpen(TenAm, ElevenPm, utc).Should().BeTrue();
    }

    [Theory]
    [InlineData(4, 29, false)]  // 09:59 IST, before opening
    [InlineData(4, 30, true)]   // 10:00 IST
    [InlineData(17, 30, true)]  // 23:00 IST
    [InlineData(17, 31, false)] // 23:01 IST
    public void IsOpen_RespectsOpeningAndClosingTimes(int utcHour, int utcMinute, bool expected)
    {
        var utc = new DateTime(2026, 10, 6, utcHour, utcMinute, 0, DateTimeKind.Utc);

        IndianTime.IsOpen(TenAm, ElevenPm, utc).Should().Be(expected);
    }

    [Fact]
    public void IsOpen_HandlesHoursPastMidnight()
    {
        var sixPm = new TimeSpan(18, 0, 0);
        var twoAm = new TimeSpan(2, 0, 0);

        IndianTime.IsOpen(sixPm, twoAm, new DateTime(2026, 10, 6, 19, 30, 0, DateTimeKind.Utc)).Should().BeTrue();  // 01:00 IST
        IndianTime.IsOpen(sixPm, twoAm, new DateTime(2026, 10, 6, 6, 30, 0, DateTimeKind.Utc)).Should().BeFalse();  // 12:00 IST
    }
}
