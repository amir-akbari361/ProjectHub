using ProjectHub.Web.Client.Http;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Auth;

/// <summary>
/// The single authority on "what access token should the next API call carry?". It hands back the stored token
/// when it is still good and performs a silent refresh when it is not.
/// </summary>
/// <remarks>
/// WHY THIS IS A SEPARATE SERVICE AND NOT LOGIC INSIDE <see cref="BearerTokenHandler"/>
/// The handler's single responsibility is to attach a header. Deciding whether a token is still valid,
/// exchanging a refresh token, and serialising concurrent attempts are three different concerns with their own
/// failure modes — folding them into a DelegatingHandler would make it the class that does everything, and
/// would make the decision untestable without a full HTTP pipeline. The handler asks a question; this answers it.
///
/// WHY REFRESH PROACTIVELY, WITH A SKEW WINDOW?
/// Without this, the first request after the access-token lifetime elapses gets a 401 and the user is bounced
/// to the login page mid-task — the single most common "why did it log me out?" complaint in a Bearer-token SPA.
/// We refresh when the token is inside <see cref="ExpirySkewSeconds"/> of expiring rather than after it has
/// expired, because a token that is valid for another 200ms will still be expired by the time it reaches the API:
/// there is network latency, and the two hosts' clocks are not identical.
///
/// WHY THE SEMAPHORE?
/// A Blazor page routinely fires several API calls at once (a project page loads the project, its tasks, and its
/// roster). If the token expired while the user was idle, all of them arrive here needing a refresh at the same
/// instant. Refresh tokens are SINGLE-USE and rotated on redemption, so the first exchange invalidates the token
/// the others are holding — they would each fail and cascade into a spurious logout. Serialising means the first
/// caller refreshes and the rest wait, then observe the already-refreshed token and proceed.
/// </remarks>
public sealed class AccessTokenProvider
{
    /// <summary>
    /// How close to expiry a token may be before we treat it as already expired. Covers request latency plus
    /// modest clock drift between the Web host and the API host.
    /// </summary>
    private const int ExpirySkewSeconds = 60;

    private readonly TokenStore _tokenStore;
    private readonly AuthApiClient _authApi;
    private readonly ILogger<AccessTokenProvider> _logger;

    // Guards the refresh exchange itself. Per-circuit (this service is scoped), which is the correct grain:
    // two different users must never queue behind each other, but one user's parallel requests must.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public AccessTokenProvider(
        TokenStore tokenStore,
        AuthApiClient authApi,
        ILogger<AccessTokenProvider> logger)
    {
        _tokenStore = tokenStore;
        _authApi = authApi;
        _logger = logger;
    }

    /// <summary>
    /// Returns a usable access token, refreshing first if necessary, or <c>null</c> when there is no live
    /// session. A null return is a normal outcome (signed out, or the refresh token has expired too) — callers
    /// send the request unauthenticated and let the API's 401 and the router's redirect take over.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        LoginResult? tokens;

        try
        {
            tokens = await _tokenStore.LoadAsync();
        }
        catch (InvalidOperationException)
        {
            // Pre-render: no browser, so no localStorage. There is nothing to attach and nothing to refresh.
            return null;
        }

        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            return null;
        }

        if (!IsExpiringSoon(tokens.AccessTokenExpiresAtUtc))
        {
            return tokens.AccessToken;
        }

        return await RefreshAsync(tokens);
    }

    private async Task<string?> RefreshAsync(LoginResult stale)
    {
        // If the refresh token is dead too there is nothing to exchange. Bail out BEFORE taking the gate so a
        // fully expired session does not serialise every pending request behind a call we know will fail.
        if (stale.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
        {
            _logger.LogInformation(
                "Refresh token expired at {ExpiresAt:o}; the session cannot be renewed.",
                stale.RefreshTokenExpiresAtUtc);
            return null;
        }

        await _refreshGate.WaitAsync();

        try
        {
            // DOUBLE-CHECK INSIDE THE GATE. While we waited, another caller may have completed the refresh.
            // Re-reading means the losers of the race use the NEW token instead of spending the (now revoked)
            // refresh token a second time, which the server would rightly reject.
            var current = await _tokenStore.LoadAsync();

            if (current is not null
                && !string.IsNullOrWhiteSpace(current.AccessToken)
                && !IsExpiringSoon(current.AccessTokenExpiresAtUtc))
            {
                return current.AccessToken;
            }

            var result = await _authApi.RefreshAsync();

            if (result.IsSuccess && result.Value is not null)
            {
                _logger.LogInformation(
                    "Access token refreshed; next expiry {ExpiresAt:o}.",
                    result.Value.AccessTokenExpiresAtUtc);
                return result.Value.AccessToken;
            }

            // AuthApiClient.RefreshAsync has already cleared the local session on failure, so the UI will
            // re-evaluate to anonymous and the router will redirect.
            _logger.LogWarning("Token refresh failed: {Error}", result.Error);
            return null;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static bool IsExpiringSoon(DateTime expiresAtUtc) =>
        expiresAtUtc <= DateTime.UtcNow.AddSeconds(ExpirySkewSeconds);
}
