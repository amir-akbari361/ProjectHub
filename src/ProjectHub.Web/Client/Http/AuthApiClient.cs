using System.Net.Http.Json;
using System.Text.Json;
using ProjectHub.Web.Client.Auth;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// Typed HTTP client for authentication endpoints. Every method returns an <see cref="ApiResult{T}"/>
/// so pages can branch on success/failure without catching. The login/register flows also persist tokens
/// to <see cref="TokenStore"/> and notify the auth state provider so the UI re-renders immediately.
/// </summary>
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

        if (!response.IsSuccessStatusCode)
        {
            var error = await ExtractErrorAsync(response);
            return ApiResult<LoginResult>.Failure(error);
        }

        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        if (result is null)
        {
            return ApiResult<LoginResult>.Failure("Invalid response from server");
        }

        // Persist tokens and notify auth state so the UI updates immediately
        await _tokenStore.SaveAsync(result);
        _authStateProvider.NotifyAuthenticationStateChanged();

        return ApiResult<LoginResult>.Success(result);
    }

    public async Task<ApiResult<RegisterResult>> RegisterAsync(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/register", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ExtractErrorAsync(response);
            return ApiResult<RegisterResult>.Failure(error);
        }

        var result = await response.Content.ReadFromJsonAsync<RegisterResult>();
        return result is not null
            ? ApiResult<RegisterResult>.Success(result)
            : ApiResult<RegisterResult>.Failure("Invalid response from server");
    }

    public async Task<ApiResult> LogoutAsync()
    {
        var tokens = await _tokenStore.LoadAsync();
        if (tokens is null)
        {
            return ApiResult.Success(); // Already logged out
        }

        // Best-effort revoke - even if it fails, we clear local tokens
        try
        {
            await _http.PostAsJsonAsync("api/auth/revoke", new RevokeRequest(tokens.RefreshToken));
        }
        catch
        {
            // Ignore network errors on logout
        }

        await _tokenStore.ClearAsync();
        _authStateProvider.NotifyAuthenticationStateChanged();

        return ApiResult.Success();
    }

    public async Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/forgot-password", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ExtractErrorAsync(response);
            return ApiResult.Failure(error);
        }

        return ApiResult.Success();
    }

    public async Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/reset-password", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ExtractErrorAsync(response);
            return ApiResult.Failure(error);
        }

        return ApiResult.Success();
    }

    /// <summary>
    /// Attempts to extract a human-readable error from a failed response. The API returns RFC 7807
    /// problem details with a "detail" field; if that's missing we fall back to the status phrase.
    /// </summary>
    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString() ?? response.ReasonPhrase ?? "Request failed";
            }
        }
        catch
        {
            // JSON parse failed, fall through
        }

        return response.ReasonPhrase ?? "Request failed";
    }
}
