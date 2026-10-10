using QrMenu.Application.Common.Exceptions;

namespace QrMenu.Application.Common;

/// <summary>A from/to period for exports (UTC). Long periods are refused so one download cannot load years of data.</summary>
public static class ReportRange
{
    public const int MaxDays = 100;

    public static void Check(DateTime from, DateTime to)
    {
        if (to <= from)
        {
            throw new ConflictException("The end of the period must be after its start.");
        }
        if ((to - from).TotalDays > MaxDays)
        {
            throw new ConflictException($"Choose a period of at most {MaxDays} days.");
        }
    }
}
