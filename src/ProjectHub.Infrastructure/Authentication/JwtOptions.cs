using System.ComponentModel.DataAnnotations;

namespace ProjectHub.Infrastructure.Authentication;

/// <summary>
/// Strongly-typed configuration for JWT issuance, bound from the "Jwt" section of appsettings via the
/// Options pattern. Using a POCO validated at startup (ValidateOnStart) means a misconfigured
/// deployment fails FAST and LOUD at boot — never silently at the first login in production. This is
/// the Options pattern's core value: configuration becomes a typed, validated, injectable dependency
/// instead of scattered <c>IConfiguration["..."]</c> string lookups that fail at runtime.
/// </summary>
/// <remarks>
/// KEY LOADING IS ENVIRONMENT-AGNOSTIC. The same binary reads the RSA private key from EITHER an
/// inline PEM string (<see cref="PrivateKeyPem"/>) OR a file path (<see cref="PrivateKeyPath"/>).
/// The environment picks which knob is filled — no code changes, no <c>if (IsDevelopment())</c>
/// branching. A <c>PostConfigure</c> step in DI reads the file into <see cref="PrivateKeyPem"/> when
/// only a path is supplied, so by the time <c>JwtProvider</c> runs it ALWAYS sees a populated PEM.
/// Typical usage:
/// <list type="bullet">
///   <item>Dev machine → <see cref="PrivateKeyPath"/> in appsettings.Development.json points at a file.</item>
///   <item>Docker / Prod → <c>Jwt__PrivateKeyPem</c> environment variable or mounted secret supplies the PEM inline.</item>
/// </list>
/// Because env vars win over JSON in .NET's config precedence, the container's inline PEM is already
/// present before PostConfigure runs, so the file read is simply skipped — same code, both worlds.
/// </remarks>
internal sealed class JwtOptions
{
    /// <summary>The configuration section name. Referenced once in DI so the magic string lives here only.</summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// The token issuer — who minted the token. Written to the "iss" claim and validated on the API
    /// side. Typically a stable URL identifying this auth service (e.g. "https://api.projecthub.com").
    /// </summary>
    [Required]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// The intended audience — who the token is FOR. Written to "aud" and validated by the API so a
    /// token minted for a different service can't be replayed here. Prevents token-confusion attacks.
    /// </summary>
    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Access-token lifetime in minutes. Kept SHORT (15 is a common default) because a leaked access
    /// token is valid until it expires — short lifetimes shrink the attack window. The long-lived
    /// refresh token (7 days) handles seamless re-issuance so users aren't logged out every 15 minutes.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenExpirationMinutes { get; init; } = 15;

    /// <summary>
    /// The RSA PRIVATE key in PEM format, used to SIGN tokens (RS256). This is ONE of two mutually
    /// exclusive sources — supply this inline in containers/production (via the <c>Jwt__PrivateKeyPem</c>
    /// environment variable or a mounted secret). It is intentionally NOT <c>[Required]</c>: on a dev
    /// machine it starts empty and is populated by the PostConfigure resolver from
    /// <see cref="PrivateKeyPath"/>. Final presence is enforced by a Validate rule in DI, so exactly
    /// one source must ultimately win — otherwise the app fails to boot.
    /// <para>
    /// Asymmetric (RS256) is deliberate over symmetric (HS256): only this auth service holds the
    /// private key, while any number of resource services hold the PUBLIC key to VERIFY without being
    /// able to MINT. HS256's single shared secret both signs and verifies — anyone who can verify can
    /// forge. RS256 cleanly separates those capabilities.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The setter is <c>internal set</c> (not <c>init</c>) on purpose: the PostConfigure key resolver
    /// in <c>DependencyInjection.ResolvePrivateKey</c> writes the file's contents here AFTER the
    /// object has been constructed and bound. An <c>init</c>-only accessor is only assignable during
    /// object initialization, which is too early for a post-configuration step; <c>internal set</c>
    /// keeps the property writable within this assembly while still hiding it from the outside world.
    /// </remarks>
    public string? PrivateKeyPem { get; internal set; }


    /// <summary>
    /// Filesystem path to a PEM file containing the RSA private key. The OTHER of the two mutually
    /// exclusive sources — used on developer machines so the key lives as a git-ignored file instead
    /// of being pasted into config. The PostConfigure resolver reads this file into
    /// <see cref="PrivateKeyPem"/> at startup IF (and only if) the inline PEM wasn't already supplied.
    /// Left null in containers, where the inline env-var PEM is preferred (no filesystem dependency,
    /// works on read-only container filesystems).
    /// </summary>
    public string? PrivateKeyPath { get; init; }
}
