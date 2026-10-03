namespace QrMenu.Application.Tables;

public record TableDto(Guid Id, string Number, int? Capacity, bool IsActive, bool HasActiveOrder);

public record CreateTableRequest(string Number, int? Capacity);

public record UpdateTableRequest(string Number, int? Capacity, bool IsActive);
