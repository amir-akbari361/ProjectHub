using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Infrastructure.Authentication;
using ProjectHub.Infrastructure.Email;
using ProjectHub.Infrastructure.Services;

namespace ProjectHub.Infrastructure;

/// <summary>
/// The Infrastructure composition root. Hosts (API, Web) call <see cref="AddInfrastructure"/> so the
/// concrete service registrations stay encapsulated here — a host references this one method, never
/// the individual implementation types, which is why those types are <c>internal</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers every Infrastructure adapter. Takes <see cref="IConfiguration"/> so it can bind and
    /// validate the strongly-typed options (<see cref="JwtOptions"/>, <see cref="EmailOptions"/>) at
    /// startup — a misconfigured secret then fails fast at boot rather than on the first login.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
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

        // Bind + resolve + validate the JWT options from the "Jwt" section.
        //   Bind                    → raw config (JSON + env vars, env wins) into the POCO.
        //   ValidateDataAnnotations → enforces [Required] Issuer/Audience and [Range] on expiry.
        //   PostConfigure           → the environment-agnostic key resolver: if no inline PEM was
        //                             supplied but a file path was, read the file INTO PrivateKeyPem.
        //                             This runs AFTER binding, so a container's Jwt__PrivateKeyPem env
        //                             var is already present and the file read is skipped automatically
        //                             — one binary, file locally / inline in Docker, no code branching.
        //   Validate                → final coherence check: exactly one source must have won and the
        //                             PEM must now be non-empty. Fails fast at boot if misconfigured.
        //   ValidateOnStart         → hoists BOTH validations to startup instead of first-login.
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .PostConfigure(ResolvePrivateKey)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.PrivateKeyPem),
                "JWT signing key missing: supply either Jwt:PrivateKeyPem (inline/env var) or a readable Jwt:PrivateKeyPath (file).")
            .ValidateOnStart();


        // Same fail-fast binding for SMTP settings.
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Stateless, thread-safe adapters: register as singletons. Each holds no per-request state —
        // they either compute (hashers, token generator) or read injected options (JWT provider).
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IJwtProvider, JwtProvider>();

        // The email sender constructs a transport per call and takes a scoped logger; scoped is the
        // conventional lifetime for an I/O adapter that participates in a request.
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }

    /// <summary>
    /// The environment-agnostic RSA key resolver, invoked by <c>PostConfigure&lt;JwtOptions&gt;</c> at
    /// startup — AFTER binding, BEFORE validation. Its whole job is to guarantee that, whatever the
    /// environment, <see cref="JwtOptions.PrivateKeyPem"/> holds the actual PEM text by the time the
    /// downstream <c>Validate</c> and <c>JwtProvider</c> run.
    /// </summary>
    /// <remarks>
    /// PRECEDENCE: an already-present inline PEM always wins. In a container, <c>Jwt__PrivateKeyPem</c>
    /// is bound before this runs, so we short-circuit and never touch the filesystem (works on
    /// read-only container images). ONLY when the inline PEM is absent do we fall back to reading the
    /// file at <see cref="JwtOptions.PrivateKeyPath"/> — the developer-machine path. If neither source
    /// yields content, we leave PrivateKeyPem null and let the <c>Validate</c> rule produce a single,
    /// clear "signing key missing" failure rather than throwing an opaque IO error here.
    /// </remarks>
    private static void ResolvePrivateKey(JwtOptions options)
    {
        // Inline PEM already supplied (env var / secret / user-secrets) → nothing to do. This is the
        // container/production path and it deliberately avoids any filesystem dependency.
        if (!string.IsNullOrWhiteSpace(options.PrivateKeyPem))
        {
            return;
        }

        // No inline PEM and no path either → leave it null; Validate will report the missing key.
        if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
        {
            return;
        }

        // A relative path (e.g. "keys/jwt-private.pem") is resolved against the app's base directory —
        // the folder the binary runs from — so it works identically under `dotnet run` and in a
        // published/container layout where the file is copied next to the DLL.
        var resolvedPath = Path.IsPathRooted(options.PrivateKeyPath)
            ? options.PrivateKeyPath
            : Path.Combine(AppContext.BaseDirectory, options.PrivateKeyPath);

        // Guard with a precise message. Without this the failure would surface as a raw
        // FileNotFoundException deep in startup; here it names the exact path we looked at.
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"JWT private key file not found. Configured Jwt:PrivateKeyPath resolved to '{resolvedPath}'.",
                resolvedPath);
        }

        // Read the PEM text into the same property the inline path would have filled. From here on the
        // rest of the system (Validate, JwtProvider) is oblivious to WHERE the key came from — the
        // whole point of the abstraction. options.PrivateKeyPem has an init-only setter, but init
        // accessors are callable from within the type's own members, which includes this static helper.
        options.PrivateKeyPem = File.ReadAllText(resolvedPath);
    }
}



