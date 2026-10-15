namespace GroceryStore.Api.Common.Errors;

/// <summary>
/// Base type for expected business failures. Services throw these; <see cref="GlobalExceptionHandler"/>
/// turns them into RFC 7807 ProblemDetails responses. Anything that is NOT an AppException is treated
/// as an unexpected server error and its details are never sent to the client.
/// </summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
    public abstract string Title { get; }
}

/// <summary>The requested resource does not exist (or the caller may not know it exists).</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string Title => "Resource not found";
}

/// <summary>The request conflicts with current state, e.g. a duplicate name or a stale edit.</summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string Title => "Conflict";
}

/// <summary>The request is well-formed but violates a business rule, e.g. insufficient stock or wallet balance.</summary>
public sealed class BusinessRuleException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string Title => "Business rule violated";
}
