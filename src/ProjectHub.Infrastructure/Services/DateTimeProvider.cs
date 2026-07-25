using ProjectHub.Application.Abstractions.Services;

namespace ProjectHub.Infrastructure.Services;

/// <summary>
/// The production implementation of <see cref="IDateTimeProvider"/>. It is the single place in the
/// system allowed to read the real wall clock, so every handler, validator, and interceptor gets
/// its "now" through the injected abstraction and can be pointed at a frozen clock in tests.
/// Backed by <see cref="TimeProvider.System"/> (the .NET 8+ testable clock) rather than
/// <c>DateTime.UtcNow</c> directly, so even this class could be unit-tested with a fake provider.
/// </summary>
internal sealed class DateTimeProvider : IDateTimeProvider
{
    // Always UTC. Persisting local time is a classic production bug: it breaks across time zones,
    // daylight-saving transitions, and servers in different regions. We store UTC and localise only
    // at the presentation edge.
    public DateTime UtcNow => TimeProvider.System.GetUtcNow().UtcDateTime;
}
