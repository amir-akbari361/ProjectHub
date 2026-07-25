using Microsoft.Extensions.DependencyInjection;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Infrastructure.Services;

namespace ProjectHub.Infrastructure;

/// <summary>
/// The Infrastructure composition root. Hosts (API, Web) call <see cref="AddInfrastructure"/> so the
/// concrete service registrations stay encapsulated here — a host references this one method, never
/// the individual implementation types, which is why those types are <c>internal</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Required for CurrentUser to reach the ambient HttpContext. Registering it here (next to its
        // only consumer) keeps the dependency co-located instead of relying on a host to remember it.
        services.AddHttpContextAccessor();

        // Stateless and thread-safe: a single shared instance serves every request.
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Scoped: it resolves per-request state (the current HttpContext), so its lifetime must not
        // outlive the request. Singleton here would be a classic captive-dependency bug — it would
        // capture the first request's context and serve it to everyone.
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
