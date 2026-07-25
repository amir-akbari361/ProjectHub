using System.Reflection;
using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectHub.Application.Behaviors;

namespace ProjectHub.Application;

/// <summary>
/// Composition root for the Application layer. The API and Web projects call
/// <see cref="AddApplication"/> and receive a fully-wired MediatR pipeline, all
/// FluentValidation validators discovered in this assembly, and a scanned Mapster config.
/// Behavior registration order matters — the first behavior registered is the outermost.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);

            // Outer -> inner. Exceptions caught outermost so every layer below is logged.
            configuration.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        var mapsterConfig = TypeAdapterConfig.GlobalSettings;
        mapsterConfig.Scan(assembly);
        services.AddSingleton(mapsterConfig);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
