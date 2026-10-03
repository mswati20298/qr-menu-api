namespace QrMenu.Application.Backgrounds;

public record BackgroundItemDto(Guid Id, string ImageUrl, int Slots, bool IsDefault);

/// <summary>Mode is "Fixed" or "TimeOfDay".</summary>
public record BackgroundSettingsDto(string Mode, List<BackgroundItemDto> Items);

public record AddBackgroundRequest(string ImageUrl);

/// <summary>Slots is a bit mask: Morning=1, Afternoon=2, Evening=4, Night=8.</summary>
public record UpdateBackgroundRequest(int Slots, bool IsDefault);

public record SetBackgroundModeRequest(string Mode);
