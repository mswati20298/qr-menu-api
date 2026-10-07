namespace QrMenu.Application.Tables;

/// <summary>QrCode = the secret printed in this table's QR link (?k=).</summary>
public record TableDto(Guid Id, string Number, int? Capacity, bool IsActive, bool HasActiveOrder, string QrCode);

public record CreateTableRequest(string Number, int? Capacity);

public record UpdateTableRequest(string Number, int? Capacity, bool IsActive);
