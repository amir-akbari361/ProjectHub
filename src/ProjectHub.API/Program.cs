using Microsoft.OpenApi.Models;
using ProjectHub.API.Infrastructure;
using ProjectHub.Application;
using ProjectHub.Infrastructure;
using ProjectHub.Persistence;
using Serilog;

// The scheme name every Bearer security reference below points at. Extracting it to a const avoids
// the "magic string" repeated across the definition and requirement — one source of truth.
const string bearerScheme = "Bearer";


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

    // Registers the MVC controller services (model binding, action invocation, etc.). We use
    // controllers rather than minimal APIs for feature endpoints because a class-per-feature groups
    // related actions, integrates cleanly with [Authorize] policies, and keeps the thin dispatch layer
    // (see ApiController) in one obvious place. The placeholder /health minimal endpoint stays as-is.
    builder.Services.AddControllers();

    // Registers the JWT Bearer authentication scheme + authorization services. Defined in Infrastructure
    // (the only layer that can see the internal JwtOptions and RSA key), so the host wires it with a
    // single call and stays ignorant of the crypto details.
    builder.Services.AddJwtAuthentication();


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

        // Declares the "Bearer" security scheme so Swagger UI shows an "Authorize" button and lets a
        // developer paste a JWT once, then have it attached to every request. Http + "bearer" is the
        // correct pairing for RFC 6750 tokens (it makes Swagger send "Authorization: Bearer <token>"
        // automatically — the developer pastes only the raw token, not the word "Bearer").
        options.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT access token (without the 'Bearer ' prefix)."
        });

        // Applies the scheme globally so the padlock appears on every operation. Endpoints marked
        // [AllowAnonymous] (like the auth endpoints) still work without a token; this only controls
        // whether Swagger OFFERS to send one.
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = bearerScheme
                    }
                },
                Array.Empty<string>()
            }
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

        // Developer convenience: hitting the root URL in Development bounces straight to the Swagger
        // UI so there's no need to remember the /swagger path. Guarded to Development so production
        // never exposes an implicit redirect to the API explorer.
        app.MapGet("/", () => Results.Redirect("/swagger"))
            .ExcludeFromDescription();
    }


    app.UseHttpsRedirection();

    // ORDER IS CRITICAL. Authentication (WHO are you? — parse & validate the JWT, build the
    // ClaimsPrincipal) must run BEFORE authorization (are you ALLOWED? — evaluate [Authorize]
    // policies against that principal). Both must run BEFORE endpoint execution so the principal
    // and the access decision exist by the time a controller action runs. Getting this order wrong
    // is the classic cause of "[Authorize] silently does nothing".
    app.UseAuthentication();
    app.UseAuthorization();

    // Liveness endpoint proving the host boots end-to-end. Left as a minimal API since it carries no
    // business logic and needs no controller.
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
        .WithName("HealthCheck")
        .WithTags("System");

    // Maps attribute-routed controllers (AuthController and every feature controller to come) into
    // the pipeline. Without this the controllers exist in DI but no route reaches them.
    app.MapControllers();

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
