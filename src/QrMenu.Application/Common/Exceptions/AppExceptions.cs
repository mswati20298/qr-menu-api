namespace QrMenu.Application.Common.Exceptions;

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class UnauthorizedAppException(string message) : Exception(message);

/// <summary>The caller is signed in but not allowed (e.g. a suspended restaurant). Code lets the UI react.</summary>
public class ForbiddenException(string message, string code = "forbidden") : Exception(message)
{
    public string Code { get; } = code;
}

public class TooManyAttemptsException(string message) : Exception(message);
