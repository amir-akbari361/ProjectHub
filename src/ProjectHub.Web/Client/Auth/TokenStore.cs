using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using ProjectHub.Web.Client.Models;

namespace ProjectHub.Web.Client.Auth;

/// <summary>
/// Persists the access/refresh token pair on the CLIENT so a browser refresh does not sign the user out.
/// </summary>
/// <remarks>
/// WHY localStorage AND NOT A COOKIE
/// The API authenticates with a Bearer JWT, not a cookie session. The app must be able to READ the access token in
/// order to attach it to the Authorization header, which an HttpOnly cookie forbids by design. localStorage is the
/// pragmatic store for a Bearer-based client. The refresh token is long-lived but single-use — rotated on every
/// redemption — which bounds the blast radius if one is stolen.
///
/// WHY EVERY METHOD IS GUARDED
/// This is an Interactive-Server app: components render once before the SignalR circuit exists, and JS interop
/// throws until it does. <see cref="AuthenticationGate"/> performs the first read at the earliest moment interop
/// is available, but these guards keep any earlier caller from taking down the render.
/// </remarks>
public sealed class TokenStore
{
    private const string StorageKey = "projecthub.tokens";

    /// <summary>
    /// Explicit serializer options rather than the defaults.
    /// </summary>
    /// <remarks>
    /// The stored blob is a PRIVATE format between this class's writes and its own reads — it is not the API's wire
    /// contract, and it lives in a browser across deployments. Pinning the options here means a future change to
    /// global JSON settings (or to how the API serializes) cannot silently make previously stored tokens
    /// unreadable, which would sign every existing user out on deploy. Case-insensitive reading additionally lets
    /// this read a blob written by an earlier version that used different casing.
    /// </remarks>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly IJSRuntime _js;
    private readonly ILogger<TokenStore> _logger;

    private LoginResult? _cached;
    private bool _loaded;

    public TokenStore(IJSRuntime js, ILogger<TokenStore> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>The current tokens, or null when signed out. Cached in memory after the first successful read.</summary>
    public LoginResult? Current => _cached;

    /// <summary>
    /// Loads the token pair from localStorage into the in-memory cache. Safe to call repeatedly — the actual read
    /// happens once. Returns null during pre-render (when interop is unavailable) WITHOUT caching, so a later call
    /// retries once the circuit is live.
    /// </summary>
    public async Task<LoginResult?> LoadAsync()
    {
        if (_loaded)
        {
            return _cached;
        }

        string? json;

        try
        {
            json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet (pre-render). Leave _loaded false so a later call retries.
            return null;
        }
        catch (JSException ex)
        {
            // localStorage itself can be unavailable — a browser configured to block site data, or a page in a
            // context where access throws rather than returning null. Treat it as "no session" and cache that, so
            // we do not retry a call that will keep failing on every request the bearer handler makes.
            _logger.LogWarning(ex, "localStorage is not accessible; treating the session as signed out.");
            _loaded = true;
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            _cached = null;
            _loaded = true;
            return null;
        }

        // WHY THIS DESERIALIZATION IS GUARDED
        // The stored value is attacker-and-accident-reachable: a user can edit it in devtools, an older version of
        // the app may have written a different shape, and a partially-written value can survive a crash. An
        // unguarded Deserialize throws JsonException, and this method is called from the authentication gate during
        // startup — where an exception would blank the page rather than simply showing the login screen. Treating
        // an unreadable blob as "signed out" and clearing it turns a dead end into a recoverable state.
        try
        {
            _cached = JsonSerializer.Deserialize<LoginResult>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Stored token blob could not be parsed; clearing it and signing out.");
            _cached = null;
            await TryRemoveAsync();
        }

        _loaded = true;
        return _cached;
    }

    /// <summary>Persists a freshly issued token pair and updates the cache.</summary>
    public async Task SaveAsync(LoginResult tokens)
    {
        // The cache is updated FIRST and unconditionally: the in-memory value is what the bearer handler reads on
        // the very next request, and it must reflect the new token even if writing to localStorage fails (which
        // would only cost persistence across a reload, not the current session).
        _cached = tokens;
        _loaded = true;

        var json = JsonSerializer.Serialize(tokens, SerializerOptions);

        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (JSException ex)
        {
            // Private-browsing quota limits and blocked site data both surface here. The session still works for
            // this circuit; it simply will not survive a reload.
            _logger.LogWarning(ex, "Could not persist tokens; the session will not survive a page reload.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Could not persist tokens: JS interop unavailable.");
        }
    }

    /// <summary>Clears the tokens on sign-out or on an unrecoverable authentication failure.</summary>
    public async Task ClearAsync()
    {
        // Cleared in memory first and regardless of what the browser does. A sign-out that left the cached token
        // live because a JS call failed would keep authenticating requests after the user asked to leave — the one
        // failure mode here that actually matters.
        _cached = null;
        _loaded = true;

        await TryRemoveAsync();
    }

    private async Task TryRemoveAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Could not remove the stored tokens from localStorage.");
        }
        catch (InvalidOperationException)
        {
            // Interop unavailable (pre-render). The in-memory cache is already cleared, which is what governs
            // whether requests are authenticated.
        }
    }
}
