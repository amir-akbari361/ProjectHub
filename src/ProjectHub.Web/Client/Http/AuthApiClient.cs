using System.Net.Http.Json;
using ProjectHub.Web.Client.Auth;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for every authentication endpoint: register, login, silent refresh, logout, email
/// confirmation, and the forgot/reset password pair.
/// </summary>
/// <remarks>
/// WHY THIS CLIENT ALONE HAS SIDE EFFECTS
/// Every other typed client is a pure transport adapter. This one additionally PERSISTS the token pair to
/// <see cref="TokenStore"/> and notifies <see cref="JwtAuthenticationStateProvider"/> on the three operations
/// that change who is signed in (login, refresh, logout). Putting that here — rather than asking each caller
/// to remember it — means there is exactly one code path that can mutate the session, so "logged in on the
/// server but not in the UI" is not a state this app can reach.
///
/// WHY IT IS REGISTERED WITHOUT <see cref="BearerTokenHandler"/>
/// These endpoints are <c>[AllowAnonymous]</c> and, more importantly, <see cref="RefreshAsync"/> is what the
/// bearer handler CALLS when a token has expired. Routing it back through that handler would be a cycle:
/// refresh → handler → needs a fresh token → refresh.
/// </remarks>
public sealed class AuthApiClient
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokenStore;
    private readonly JwtAuthenticationStateProvider _authStateProvider;

    public AuthApiClient(
        HttpClient http,
        TokenStore tokenStore,
        JwtAuthenticationStateProvider authStateProvider)
    {
        _http = http;
        _tokenStore = tokenStore;
        _authStateProvider = authStateProvider;
    }

    public async Task<ApiResult<LoginResult>> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", request);
        var result = await response.ToResultAsync<LoginResult>();

        if (result.IsSuccess && result.Value is not null)
        {
            await PersistSessionAsync(result.Value);
        }

        return result;
    }

    public async Task<ApiResult<RegisterResult>> RegisterAsync(RegisterRequest request)
    {
        // Registration deliberately does NOT sign the user in. The API issues no tokens here because the
        // account may still need email confirmation, so the UI sends them to /login afterwards.
        var response = await _http.PostAsJsonAsync("api/auth/register", request);
        return await response.ToResultAsync<RegisterResult>();
    }

    /// <summary>
    /// Exchanges the stored refresh token for a NEW token pair and persists it.
    /// </summary>
    /// <remarks>
    /// WHY FAILURE CLEARS THE SESSION
    /// The API rotates refresh tokens: a successful exchange revokes the one presented. So a failure here means
    /// the token is expired, unknown, or already spent — in every case the session is unrecoverable and the
    /// only honest response is to sign the user out locally. Leaving the dead tokens in place would produce a
    /// UI that looks authenticated while every API call 401s.
    /// </remarks>
    public async Task<ApiResult<LoginResult>> RefreshAsync()
    {
        var tokens = await _tokenStore.LoadAsync();

        if (tokens is null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return ApiResult<LoginResult>.Failure("There is no session to refresh.");
        }

        var response = await _http.PostAsJsonAsync(
            "api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        var result = await response.ToResultAsync<LoginResult>();

        if (result.IsSuccess && result.Value is not null)
        {
            await PersistSessionAsync(result.Value);
            return result;
        }

        await ClearSessionAsync();
        return result;
    }

    /// <summary>
    /// Signs the user out: revokes the refresh token server-side, then clears the local session.
    /// </summary>
    /// <remarks>
    /// The revoke call is BEST-EFFORT and its failure is swallowed. A user who clicks "sign out" must end up
    /// signed out of this browser even if the network is down — otherwise a transient error leaves them
    /// stranded in a session they explicitly asked to end. The server-side token then simply expires on its own
    /// schedule, which is the safer of the two failure modes.
    /// </remarks>
    public async Task<ApiResult> LogoutAsync()
    {
        var tokens = await _tokenStore.LoadAsync();

        if (tokens is null)
        {
            return ApiResult.Success();
        }

        try
        {
            await _http.PostAsJsonAsync("api/auth/revoke", new RevokeRequest(tokens.RefreshToken));
        }
        catch (HttpRequestException)
        {
            // Network unreachable. Fall through and clear locally — see the remarks above.
        }
        catch (TaskCanceledException)
        {
            // Request timed out. Same reasoning.
        }

        await ClearSessionAsync();
        return ApiResult.Success();
    }

    public async Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/forgot-password", request);
        return await response.ToResultAsync();
    }

    public async Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/reset-password", request);
        return await response.ToResultAsync();
    }

    /// <summary>
    /// Redeems the one-time token from an activation email.
    /// </summary>
    /// <remarks>
    /// The API takes the token from the QUERY STRING (<c>POST api/auth/confirm-email?token=…</c>) and has no
    /// request body, because the value arrives embedded in a link the user clicks. We escape it: a base64-ish
    /// Identity token routinely contains <c>+</c> and <c>/</c>, and an unescaped <c>+</c> decodes to a space on
    /// the server — the classic reason "the confirmation link says invalid token" for a token that is fine.
    /// </remarks>
    public async Task<ApiResult> ConfirmEmailAsync(string token)
    {
        var response = await _http.PostAsync(
            $"api/auth/confirm-email?token={Uri.EscapeDataString(token)}", content: null);
        return await response.ToResultAsync();
    }

    private async Task PersistSessionAsync(LoginResult tokens)
    {
        await _tokenStore.SaveAsync(tokens);
        _authStateProvider.NotifyAuthenticationStateChanged();
    }

    private async Task ClearSessionAsync()
    {
        await _tokenStore.ClearAsync();
        _authStateProvider.NotifyAuthenticationStateChanged();
    }
}
