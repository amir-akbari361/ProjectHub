using Microsoft.JSInterop;
using ProjectHub.Web.Client.Models;
using System.Text.Json;

namespace ProjectHub.Web.Client.Auth;

/// <summary>
/// Persists the access/refresh token pair on the CLIENT so a browser refresh doesn't log the user out.
/// Because this is an Interactive-Server Blazor app, we can't touch <c>localStorage</c> during
/// pre-rendering (there's no DOM yet), so every method is guarded to no-op until JS interop is available.
/// </summary>
/// <remarks>
/// WHY localStorage AND NOT A COOKIE?
/// The API authenticates with a Bearer JWT, not a cookie session. The SPA must be able to READ the
/// access token to attach it to the <c>Authorization</c> header, which an HttpOnly cookie forbids by
/// design. localStorage is the pragmatic store for a Bearer-based SPA. The refresh token is long-lived
/// but single-use (rotated on every refresh), which limits the blast radius of theft.
/// </remarks>
public sealed class TokenStore
{
    private const string StorageKey = "projecthub.tokens";
    private readonly IJSRuntime _js;

    private LoginResult? _cached;
    private bool _loaded;

    public TokenStore(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>The current tokens, or null if the user is signed out. Cached in-memory after first read.</summary>
    public LoginResult? Current => _cached;

    /// <summary>
    /// Loads tokens from localStorage into the in-memory cache. Safe to call repeatedly; the actual
    /// read happens once. Returns null (and stays a no-op) during pre-render when JS isn't ready.
    /// </summary>
    public async Task<LoginResult?> LoadAsync()
    {
        if (_loaded)
        {
            return _cached;
        }

        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            _cached = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<LoginResult>(json);
            _loaded = true;
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet (pre-render). Leave uncached so a later call retries.
        }

        return _cached;
    }

    /// <summary>Persists a freshly-issued token pair and updates the cache.</summary>
    public async Task SaveAsync(LoginResult tokens)
    {
        _cached = tokens;
        _loaded = true;
        var json = JsonSerializer.Serialize(tokens);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    /// <summary>Clears tokens on logout / auth failure.</summary>
    public async Task ClearAsync()
    {
        _cached = null;
        _loaded = true;
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}
