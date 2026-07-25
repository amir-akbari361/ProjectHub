namespace ProjectHub.Application.Common;

/// <summary>
/// Represents an application-level failure. Errors are values (not exceptions) so the caller
/// can pattern-match on them and translate them into HTTP problem details, validation errors, etc.
/// Modeled as a <c>readonly record struct</c> for zero-allocation equality and pattern-matching.
/// </summary>
public readonly record struct Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Sentinel value representing "no error". Prefer using <see cref="Result.Success"/> instead of returning this directly.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>Represents a missing null payload where a value was required.</summary>
    public static readonly Error NullValue = new("General.Null", "A null value was provided.", ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}

/// <summary>
/// Discriminates error categories so upper layers (e.g., an ASP.NET exception middleware) can
/// map them to the correct HTTP status without leaking implementation details.
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5
}
