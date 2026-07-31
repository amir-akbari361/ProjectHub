using Microsoft.OpenApi.Models;
using ProjectHub.API.Infrastructure;
using ProjectHub.Application;
using ProjectHub.Infrastructure;
using ProjectHub.Persistence;
using Serilog;

// ---------------------------------------------------------------------------------------------
// Bootstrap logger. Created BEFORE the host so that failures during host construction itself
// (bad config, DI errors) are captured. It is replaced by the fully-configured logger below.
// ---------------------------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Read Serilog's full configuration from appsettings so log levels/sinks are changeable
    // without recompiling. ReadFrom.Services lets registered enrichers participate.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // -----------------------------------------------------------------------------------------
    // Composition root: the API is the ONLY place that wires all executable layers together.
    // Each AddXxx() encapsulates its own layer's registrations (SRP for DI configuration).
    // -----------------------------------------------------------------------------------------
    builder.Services.AddApplication();
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);


    // Framework-native exception handling: our IExceptionHandler + the built-in ProblemDetails
    // service together produce RFC 7807 responses. AddProblemDetails() must be present or
    // TryWriteAsync has nothing to write with.
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ProjectHub API",
            Version = "v1",
            Description = "Enterprise Project Management System — REST API."
        });
    });

    var app = builder.Build();

    // Logs one structured line per request (method, path, status, elapsed ms) instead of the
    // default noisy multi-line output. Sits early so it wraps the whole pipeline.
    app.UseSerilogRequestLogging();

    // Wires our GlobalExceptionHandler. With an empty options lambda it delegates entirely to the
    // registered IExceptionHandler, so no fallback error page is needed.
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // Placeholder liveness endpoint proving the host boots end-to-end. Real feature endpoints
    // (controllers/minimal APIs) arrive with their vertical slices starting at Feature 6.
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
        .WithName("HealthCheck")
        .WithTags("System");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly during startup.");
}
finally
{
    Log.CloseAndFlush();
}
