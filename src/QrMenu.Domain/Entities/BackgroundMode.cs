namespace QrMenu.Domain.Entities;

public enum BackgroundMode
{
    /// <summary>Always show the default background image.</summary>
    Fixed = 0,

    /// <summary>Show the image assigned to the current time of day (falls back to the default).</summary>
    TimeOfDay = 1
}
