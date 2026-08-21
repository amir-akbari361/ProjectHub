using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProjectHub.Application.Abstractions.Authentication;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Entities;

namespace ProjectHub.Infrastructure.Authentication;

/// <summary>
/// RSA (RS256) adapter for <see cref="IJwtProvider"/>. Mints signed JWT access tokens embedding the
/// user's identity and role claims. This is the ONLY place in the whole system that touches the JWT
/// signing library — every other layer speaks the <see cref="IJwtProvider"/> port. If we ever swap
/// RS256 for EdDSA, move to a hardware HSM, or add key rotation, this single file changes.
/// </summary>
/// <remarks>
/// Why RS256 (asymmetric) over HS256 (symmetric)? See <see cref="JwtOptions.PrivateKeyPem"/> — the
/// short version is separation of powers: this service SIGNS with the private key; resource APIs
/// VERIFY with the public key and can never forge. The <c>RSA</c> instance is created once per call
/// from the configured PEM; for very high throughput you'd cache it, but token minting only happens on
/// login/refresh (not per request), so the clarity of a self-contained method wins here.
/// </remarks>
internal sealed class JwtProvider : IJwtProvider
{
    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <summary>
    /// <paramref name="options"/> arrives as <see cref="IOptions{TOptions}"/> — the Options pattern —
    /// so the validated, bound <see cref="JwtOptions"/> is injected rather than raw configuration. We
    /// take <see cref="IDateTimeProvider"/> instead of calling <c>DateTime.UtcNow</c> directly so token
    /// expiry is deterministic and unit-testable (a fake clock lets a test assert exact expiry values).
    /// </summary>
    public JwtProvider(IOptions<JwtOptions> options, IDateTimeProvider dateTimeProvider)
    {
        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public AccessToken GenerateAccessToken(User user)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var expiresAtUtc = utcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

        // Build the claim set — the token's payload. Each claim is a fact the API can trust because
        // the signature guarantees the token wasn't tampered with.
        var claims = new List<Claim>
        {
            // "sub" (subject): the canonical user id. This is what the API reads to know WHO is calling.
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

            // "jti" (JWT id): a unique per-token id. Enables optional server-side revocation/denylisting
            // and makes each token distinguishable in logs and audits.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            // "email": convenience claim so the API/UI can show the user without a DB round-trip.
            new(JwtRegisteredClaimNames.Email, user.Email.Value)
        };

        // Role claims drive role-based authorization ([Authorize(Roles = "Admin")]). We emit one
        // ClaimTypes.Role per assigned role; the ASP.NET Core JWT handler maps these into the
        // ClaimsPrincipal so [Authorize] and policies work out of the box. Note we key off RoleId —
        // the token carries stable identifiers, and richer role data stays server-side.
        foreach (var userRole in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, userRole.RoleId.ToString()));
        }

        // Import the RSA private key from PEM. `using` disposes the key material promptly so it doesn't
        // linger in memory longer than needed. ImportFromPem reads the PKCS#8/PKCS#1 PEM text directly.
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_options.PrivateKeyPem);

        var securityKey = new RsaSecurityKey(rsa)
        {
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false 
            }
        };

        // Wrap the key in signing credentials using RSA-SHA256. RsaSecurityKey adapts the RSA instance
        // to the token library's key abstraction; SecurityAlgorithms.RsaSha256 selects RS256.
        var signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.RsaSha256);

        // Assemble the token descriptor: issuer/audience (validated on the API side), the claims
        // identity, absolute expiry, and the signing credentials. NotBefore defaults to now.
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc,
            SigningCredentials = signingCredentials
        };

        // Create and serialize the token to its compact "header.payload.signature" form. The handler
        // performs the actual RSA signing over the header+payload here.
        var handler = new JwtSecurityTokenHandler();
        var securityToken = handler.CreateToken(tokenDescriptor);
        var tokenValue = handler.WriteToken(securityToken);

        // Return the encoded token plus its absolute expiry so the Application layer can hand both to
        // the client — the client uses the expiry to refresh proactively before the token lapses.
        return new AccessToken(tokenValue, expiresAtUtc);
    }
}