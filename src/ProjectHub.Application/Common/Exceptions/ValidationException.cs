using FluentValidation.Results;

namespace ProjectHub.Application.Common.Exceptions;

/// <summary>
/// Thrown by the <c>ValidationBehavior</c> when a request fails FluentValidation checks.
/// Carries a per-property dictionary of error messages so the API layer can map it to
/// RFC 7807 <c>ValidationProblemDetails</c> without re-inspecting individual failures.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures have occurred.")
    {
        Errors = failures
            .GroupBy(failure => failure.PropertyName, failure => failure.ErrorMessage)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
