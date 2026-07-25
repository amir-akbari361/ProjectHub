using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Persistence.Interceptors;
using ProjectHub.Persistence.Repositories;

namespace ProjectHub.Persistence;

/// <summary>
/// Composition root for the Persistence layer. The API and Web hosts call <see cref="AddPersistence"/>
/// and receive a fully-wired EF Core stack: the <see cref="ApplicationDbContext"/> bound to SQL Server,
/// the two cross-cutting save interceptors, the generic write-side repository, and the unit of work.
/// Everything is registered against the Application-owned abstractions, so the upper layers stay
/// ignorant of EF Core — this is the only assembly that knows a database exists.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Read the connection string by name from configuration rather than hard-coding it. The
        // value differs per environment (Development/Production/Docker) but the code never changes.
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'Database' was not found. Configure it in appsettings or the environment.");

        // Interceptors carry scoped dependencies (IDateTimeProvider, IPublisher), so they must be
        // registered in the container rather than newed up — otherwise those dependencies could not
        // be injected and their lifetimes would not be honoured.
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<PublishDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(connectionString, sqlServer =>
            {
                // Keep EF's migrations history table inside our own schema instead of dbo, so a
                // shared database stays tidy and permissions can be scoped per bounded context.
                sqlServer.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");

                // Transparent, capped retries ride out transient SQL Server faults (deadlocks,
                // brief connection drops) without the caller writing any retry code.
                sqlServer.EnableRetryOnFailure();
            });

            // Resolve the interceptors from the request scope so their scoped dependencies are the
            // same instances the rest of the request uses (one clock, one MediatR publisher).
            options.AddInterceptors(
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>(),
                serviceProvider.GetRequiredService<PublishDomainEventsInterceptor>());
        });

        // Expose the context to the Application layer only through its read-only abstraction. Query
        // handlers depend on IApplicationDbContext, never on the concrete ApplicationDbContext.
        services.AddScoped<IApplicationDbContext>(
            serviceProvider => serviceProvider.GetRequiredService<ApplicationDbContext>());

        // Open-generic registration: one line wires IRepository<T> for every aggregate root, so we
