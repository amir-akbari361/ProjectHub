using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ProjectHub.Infrastructure.Authentication;

/// <summary>
/// The VERIFICATION counterpart to <see cref="JwtProvider"/>. While JwtProvider SIGNS tokens using the
/// RSA PRIVATE key, this class configures the ASP.NET Core JWT Bearer handler to VERIFY incoming
/// tokens using the RSA PUBLIC key. An <see cref="RSA"/> instance imported from a private-key PEM
/// already contains the public components, so we can reuse it as the <see cref="RsaSecurityKey"/> for
/// validation. In a true multi-service architecture only the public key would ship here; in this
/// single-process app the same key material serves both halves, and that is a deliberate trade-off.
/// </summary>
/// <remarks>
/// WHY <see cref="IConfigureOptions{TOptions}"/> AND NOT INLINE SETUP?
/// The JWT bearer options depend on the validated <see cref="JwtOptions"/>, which isn't available at
/// <c>AddJwtBearer()</c> time — it is only fully resolved and validated during container build. By
/// registering this configurator with <c>ConfigureOptions&lt;ConfigureJwtBearerOptions&gt;()</c>, we
/// let DI inject <c>IOptions&lt;JwtOptions&gt;</c> and build the RSA key ONCE at first resolution,
/// rather than per request. The configuration runs the first time bearer auth is used; by then the
/// validated <c>JwtOptions</c> (with its loaded PEM) is guaranteed to exist.
/// </remarks>
internal sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwtOptions;

    /// <summary>
    /// Inject the validated <see cref="JwtOptions"/> (which was bound, file-resolved, and validated
    /// during startup). By the time this constructor runs the PEM is guaranteed to be populated.
    /// </summary>
    public ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Named-options hook. We only configure the DEFAULT scheme (JwtBearerDefaults.AuthenticationScheme),
    /// so we guard on the scheme name and no-op for others. If the host ever adds a second named Bearer
    /// scheme (unlikely), this configurator won't touch it.
    /// </summary>
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        Configure(options);
    }

    /// <summary>
    /// Unnamed fallback. ASP.NET Core calls whichever overload matches the registration; having both
    /// ensures the configurator works whether called as named or unnamed.
    /// </summary>
    public void Configure(JwtBearerOptions options)
    {
        // Import the SAME RSA private key PEM the JwtProvider uses for signing. An RSA instance imported
        // from a private-key PEM already contains the corresponding public components (modulus + exponent),
        // which is all the verifier needs. In a microservices topology only the public key would be
        // shipped here (via a .pub PEM or JWKS endpoint); in this single-process app it is simpler —
        // and still secure — to reuse the same PEM for both signing and verification.
        var rsa = RSA.Create();
        rsa.ImportFromPem(_jwtOptions.PrivateKeyPem!);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // ISSUER VALIDATION: ensures the token was minted by this auth service (not another). The
            // "iss" claim must exactly match our configured Issuer. Without this an attacker could mint
            // a token with their own key and a forged "iss", bypassing signature validation if the API
            // accepted multiple issuers.
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,

            // AUDIENCE VALIDATION: ensures the token was minted FOR this API (not another). The "aud"
            // claim must exactly match our configured Audience. Without this a token intended for a
            // different service could be replayed here (a "confused deputy" attack).
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,

            // LIFETIME VALIDATION: ensures the token hasn't expired ("exp" claim) and isn't being used
            // before its "not before" time ("nbf" claim). This is the core time-based revocation: a
            // 15-minute access token can't be used after 15 minutes, limiting the blast radius of a leak.
            ValidateLifetime = true,

            // SIGNATURE VALIDATION: verifies the token was signed with the private key corresponding to
            // this public key. The RSA signature proves that only someone holding the private key could
            // have minted this token — and since we never ship the private key anywhere, that someone
            // can only be our JwtProvider. This is the cryptographic root of trust.
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsa),

            // CLOCK SKEW: the default is 5 minutes, meaning a token is still accepted for 5 minutes AFTER
            // its "exp". That would silently extend a 15-minute token's real lifetime by a third,
            // undermining the short-lifetime security model. We set it to zero so a token expires exactly
            // when its "exp" says. In a true distributed system a small skew accounts for clock drift
            // between services; in this single-process app clocks are synchronized by definition.
            ClockSkew = TimeSpan.Zero,

            // NAME CLAIM TYPE: maps the "sub" (subject) claim to ClaimTypes.NameIdentifier, so downstream
            // code can read the user id via User.Identity.Name or User.FindFirst(ClaimTypes.NameIdentifier)
            // — the conventional .NET approach. Without this the "sub" would be accessible only via its
            // raw JWT claim name, breaking framework conventions.
            NameClaimType = "sub"
        };
    }
}
