namespace ProjectHub.Application.Abstractions.Services;

/// <summary>
/// Abstracts the clock so handlers, validators, and domain calls receive a testable UTC "now".
/// Never use <c>DateTime.UtcNow</c> directly in Application/Domain — it hard-couples code to
/// wall-clock time and makes deterministic tests (frozen time, time travel) impossible.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
