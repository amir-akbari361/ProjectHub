using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Application.Abstractions.Storage;
using ProjectHub.Infrastructure.Authentication;
using ProjectHub.Infrastructure.Email;
using ProjectHub.Infrastructure.Services;
using ProjectHub.Infrastructure.Storage;

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

        // Bind + validate the file-storage root at startup (same fail-fast discipline as JWT/SMTP).
        services
            .AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The local disk adapter holds no per-request state (its only field is the immutable root path
        // resolved once in the constructor) and every operation is a fresh, independent I/O call, so a
        // single shared Singleton instance is both safe and cheapest. Callers depend on IFileStorage,
        // never the concrete type — swapping to a cloud adapter is a one-line change right here.
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }

    /// <summary>
    /// Registers the JWT Bearer authentication scheme so the API can VERIFY incoming access tokens.
    /// This lives in Infrastructure — not the host — because it is the only layer that can see the
    /// <see cref="JwtOptions"/> (and thus the RSA key), which are deliberately <c>internal</c>. The host
    /// simply calls <c>AddJwtAuthentication()</c>, keeping the crypto/config details encapsulated.
    /// </summary>
    /// <remarks>
    /// SIGN vs VERIFY — the two halves of RS256:
    ///  • <see cref="JwtProvider"/> SIGNS with the RSA PRIVATE key.
    ///  • This handler VERIFIES with the RSA PUBLIC key.
    /// An <see cref="RSA"/> instance imported from a private-key PEM already contains the public
    /// components, so we can reuse it as the <see cref="RsaSecurityKey"/> for validation. In a true
    /// multi-service topology only the public key would be shipped here; in this single-process app the
    /// same key material serves both, and that is a deliberate, documented simplification.
    ///
    /// We validate EVERY dimension of the token (issuer, audience, lifetime, signature) and set
    /// <c>ClockSkew = TimeSpan.Zero</c> so a token expires exactly when its "exp" says — the default 5-minute
    /// skew would silently extend a 15-minute access token's real lifetime by a third, undermining the
    /// short-lifetime security model. NameClaimType maps "sub" to <c>User.Identity.Name</c>/<c>ClaimTypes.NameIdentifier</c>
    /// so downstream code reads the user id the conventional way.
    /// </remarks>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Bearer options depend on the validated JwtOptions, which aren't available until the container
        // is built. ConfigureOptions with an IConfigureOptions<JwtBearerOptions> lets us inject
        // IOptions<JwtOptions> and build the RSA key ONCE, at first resolution, rather than per request.
        services.ConfigureOptions<ConfigureJwtBearerOptions>();

        // Authorization services back the [Authorize] attribute and any policies the host declares.
        services.AddAuthorization();

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



