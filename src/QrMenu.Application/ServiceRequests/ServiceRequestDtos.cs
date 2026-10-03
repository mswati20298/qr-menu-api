namespace QrMenu.Application.ServiceRequests;

/// <summary>Type is "CallWaiter", "Water" or "Bill". Status is "Pending" or "Done".</summary>
public record ServiceRequestDto(Guid Id, string TableNumber, string Type, string Status, DateTime CreatedAt);

public record CreateServiceRequest(string TableNumber, string Type);
