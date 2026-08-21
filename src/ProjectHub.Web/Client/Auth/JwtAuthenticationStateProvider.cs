using Microsoft.AspNetCore.Components.Authorization;
using ProjectHub.Web.Client.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ProjectHub.Web.Client.Auth;


/// <summary>
/// Bridges our Bearer-token world into Blazor's authentication abstraction. Blazor components use
/// <c>&lt;AuthorizeView&gt;</c> and the <c>[Authorize]</c> router pattern, both of which read a
/// <see cref="ClaimsPrincipal"/> from an <see cref="AuthenticationStateProvider"/>. We derive that
/// principal by DECODING the JWT we already hold in <see cref="TokenStore"/> — no extra server call.
/// </summary>
/// <remarks>
/// WHY DECODE THE JWT CLIENT-SIDE INSTEAD OF CALLING A "/me" ENDPOINT?
/// The access token already carries the identity claims (sub, email, role) the UI needs to decide what
/// to show. Reading them locally avoids a round-trip on every navigation. This is a UI convenience only —
/// the API still re-validates the token's SIGNATURE on every request, so a tampered client-side claim
/// grants nothing. Never trust these claims for authorization decisions on the server.
/// </remarks>
public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly TokenStore _tokenStore;

    public JwtAuthenticationStateProvider(TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // WHY THE TRY/CATCH AROUND LoadAsync?
        // In an Interactive-Server Blazor app this method is invoked twice: once during the initial
        // *pre-render* (static SSR) when there is no browser yet, and again once the SignalR circuit is
        // live and JS interop works. TokenStore.LoadAsync reads localStorage via JS, which THROWS
        // InvalidOperationException during pre-render. If that exception escaped here it would bubble up
        // through CascadingAuthenticationState and blank the page. Catching it and returning Anonymous
        // makes pre-render deterministically "not signed in"; the SECOND call (post-interop) then reads
        // the real token and, because TokenStore didn't cache the failed read, resolves the true identity.
        LoginResult? tokens;
        try
        {
            tokens = await _tokenStore.LoadAsync();
        }
        catch (InvalidOperationException)
        {
            // JS interop unavailable (pre-render). Treat as anonymous for now; a re-evaluation happens
            // after the circuit connects.
            return Anonymous;
        }

        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            return Anonymous;
        }


        // If the access token is already past its expiry there's no point presenting it — treat the
        // user as anonymous so the UI routes to login rather than firing doomed API calls.
        if (tokens.AccessTokenExpiresAtUtc <= DateTime.UtcNow)
        {
            return Anonymous;
        }

        var claims = ParseClaims(tokens.AccessToken);
        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Called by the login/logout flows to push a fresh authentication state to every subscribed
    /// component so the UI re-renders (nav bar, authorize views) without a full reload.
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Decodes the JWT payload into .NET claims, normalizing the common role and name-identifier
    /// claim URIs so <c>context.User.IsInRole(...)</c> and <c>User.FindFirst(ClaimTypes.NameIdentifier)</c>
    /// work as components expect.
    /// </summary>
    private static IEnumerable<Claim> ParseClaims(string accessToken)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var claims = new List<Claim>();

        foreach (var claim in token.Claims)
        {
            var type = claim.Type switch
            {
                "sub" => ClaimTypes.NameIdentifier,
                "email" => ClaimTypes.Email,
                "role" => ClaimTypes.Role,
                _ => claim.Type
            };
            claims.Add(new Claim(type, claim.Value));
        }

        return claims;
    }
}
