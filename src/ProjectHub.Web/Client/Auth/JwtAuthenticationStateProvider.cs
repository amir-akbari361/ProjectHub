using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Auth;

/// <summary>
/// Bridges our Bearer-token world into Blazor's authentication abstraction. <c>&lt;AuthorizeView&gt;</c>, the
/// <c>[Authorize]</c> router pattern, and <c>AuthorizeRouteView</c> all read a <see cref="ClaimsPrincipal"/>
/// from an <see cref="AuthenticationStateProvider"/>; we derive that principal by DECODING the JWT already held
/// in <see cref="TokenStore"/>, with no extra server call.
/// </summary>
/// <remarks>
/// WHY DECODE THE JWT CLIENT-SIDE INSTEAD OF CALLING A "/me" ENDPOINT?
/// The access token already carries the identity claims (sub, email, role) the UI needs in order to decide what
/// to show. Reading them locally avoids a round-trip on every navigation. This is a UI convenience ONLY — the API
/// re-validates the token's signature on every request, so a claim tampered with in the browser grants nothing.
/// Never treat these claims as an authorization decision; they only choose what to render.
///
/// WHY SESSION LIVENESS IS JUDGED ON THE REFRESH TOKEN, NOT THE ACCESS TOKEN
/// This provider used to report Anonymous the moment the ACCESS token expired. That is wrong: an expired access
/// token beside a valid refresh token is a perfectly live session — the pair exists precisely so the short-lived
/// half can lapse. Treating it as signed-out redirected users to /login every time their access token aged out,
/// discarding a session the server would happily have renewed. So the refresh token's expiry decides whether the
/// user is signed in, and <see cref="AccessTokenProvider"/> quietly renews the access half on the next API call.
///
/// Claims are read from the access token even when it is a minute past expiry: an expired signature still carries
/// the user's own id and name, and the alternative (rendering a signed-in user as anonymous for one render pass)
/// is a visible flicker for no security gain, since the API is the only thing that can be fooled and it checks
/// the signature itself.
/// </remarks>
public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly TokenStore _tokenStore;
    private readonly ILogger<JwtAuthenticationStateProvider> _logger;

    public JwtAuthenticationStateProvider(
        TokenStore tokenStore,
        ILogger<JwtAuthenticationStateProvider> logger)
    {
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // WHY THE TRY/CATCH AROUND LoadAsync?
        // In an Interactive-Server app this method runs twice: once during the initial static render, when there
        // is no browser and JS interop THROWS, and again once the SignalR circuit is live. Letting that exception
        // escape would bubble through CascadingAuthenticationState and blank the page. Catching it makes the first
        // pass deterministically "not signed in"; because TokenStore does not cache a failed read, the second pass
        // (after AuthenticationGate has read the token and re-notified) resolves the real identity.
        LoginResult? tokens;

        try
        {
            tokens = await _tokenStore.LoadAsync();
        }
        catch (InvalidOperationException)
        {
            return Anonymous;
        }

        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            return Anonymous;
        }

        // The refresh token is the session's real lifetime. Once IT has lapsed nothing can be renewed, so the
        // user genuinely is signed out and the router should send them to /login.
        if (tokens.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
        {
            return Anonymous;
        }

        var claims = TryParseClaims(tokens.AccessToken);

        if (claims is null)
        {
            // A token we cannot decode is unusable — corrupt storage, or a format change. Treat it as no session
            // rather than presenting a half-built principal that components would read empty claims from.
            return Anonymous;
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Pushes a freshly evaluated authentication state to every subscriber so the UI re-renders (nav bar, authorize
    /// views, the router) without a full reload. Called by the login/refresh/logout flows.
    /// </summary>
    public void NotifyAuthenticationStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    /// <summary>
    /// Decodes the JWT payload into .NET claims, normalising the short JWT claim names onto the URIs that
    /// <c>User.IsInRole(...)</c> and <c>User.FindFirst(ClaimTypes.NameIdentifier)</c> expect. Returns null if the
    /// token cannot be read at all.
    /// </summary>
    private IReadOnlyList<Claim>? TryParseClaims(string accessToken)
    {
        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

            return token.Claims
                .Select(claim => new Claim(NormalizeClaimType(claim.Type), claim.Value))
                .ToList();
        }
        catch (ArgumentException ex)
        {
            // ReadJwtToken throws ArgumentException for a malformed token (wrong segment count, bad base64).
            _logger.LogWarning(ex, "Stored access token could not be decoded; treating the session as anonymous.");
            return null;
        }
    }

    /// <summary>
    /// Maps the compact JWT claim names the API emits onto the <see cref="ClaimTypes"/> URIs the framework's
    /// authorization helpers look for. Anything unrecognised is passed through untouched so custom claims survive.
    /// </summary>
    private static string NormalizeClaimType(string jwtClaimType) => jwtClaimType switch
    {
        JwtRegisteredClaimNames.Subject => ClaimTypes.NameIdentifier,
        JwtRegisteredClaimNames.Email => ClaimTypes.Email,
        JwtRegisteredClaimNames.Role => ClaimTypes.Role,
        JwtRegisteredClaimNames.GivenName => ClaimTypes.GivenName,
        JwtRegisteredClaimNames.FamilyName => ClaimTypes.Surname,
        JwtRegisteredClaimNames.Name => ClaimTypes.Name,
        _ => jwtClaimType
    };

    /// <summary>
    /// The raw JWT claim names this app translates. Spelled out as constants rather than inline literals so the
    /// mapping above reads as a table and a typo in one of them is not a silent authorization hole (a mistyped
    /// "role" would simply never map, and every role check would quietly fail closed).
    /// </summary>
    private static class JwtRegisteredClaimNames
    {
        public const string Subject = "sub";
        public const string Email = "email";
        public const string Role = "role";
        public const string GivenName = "given_name";
        public const string FamilyName = "family_name";
        public const string Name = "name";
    }
}
