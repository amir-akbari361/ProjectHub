using Microsoft.JSInterop;

namespace ProjectHub.Web.Client.Theme;

/// <summary>
/// Persists the user's light/dark choice in <c>localStorage</c> so it survives a reload, and falls back to
/// the operating system preference the first time we see a visitor.
/// </summary>
/// <remarks>
/// WHY A SERVICE INSTEAD OF A FIELD ON THE LAYOUT?
/// Both <c>MainLayout</c> and <c>AuthLayout</c> host a theme provider, and a user who flips to light mode on
/// the sign-in screen expects the app to still be light after they authenticate. A bool held in one layout's
/// state cannot survive the hop between layouts (they are different component instances), so the preference
/// has to live outside the component tree. Scoped-per-circuit is the right lifetime: it is a per-user
/// setting, and the in-memory cache means only the first read pays for JS interop.
///
/// WHY IS EVERY READ GUARDED?
/// This is an Interactive-Server app: components render once before the SignalR circuit exists, and JS
/// interop throws until it does. Returning the last known value instead of propagating that exception keeps
/// the first paint deterministic rather than blanking the page.
/// </remarks>
public sealed class ThemePreferenceStore
{
    private const string StorageKey = "projecthub.theme";
    private const string DarkValue = "dark";
    private const string LightValue = "light";

    private readonly IJSRuntime _js;

    private bool? _cached;

    public ThemePreferenceStore(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// The resolved preference: the stored choice if the user has made one, otherwise the OS setting.
    /// <paramref name="systemPrefersDark"/> is supplied by the caller because only the theme provider can
    /// answer it, and this store deliberately knows nothing about MudBlazor.
    /// </summary>
    public async Task<bool> GetIsDarkModeAsync(bool systemPrefersDark)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);

            _cached = stored switch
            {
                DarkValue => true,
                LightValue => false,
                // No stored choice yet — honour the OS, but do NOT write it back. Persisting only an
                // explicit click means a user who later changes their OS theme still gets followed.
                _ => systemPrefersDark
            };
        }
        catch (InvalidOperationException)
        {
            // Pre-render: interop unavailable. Leave _cached unset so the next call retries once the
            // circuit is live, and answer with the system preference for this pass.
            return systemPrefersDark;
        }

        return _cached.Value;
    }

    /// <summary>Records an explicit user choice and returns the new value for immediate binding.</summary>
    public async Task<bool> SetIsDarkModeAsync(bool isDarkMode)
    {
        _cached = isDarkMode;

        try
        {
            await _js.InvokeVoidAsync(
                "localStorage.setItem", StorageKey, isDarkMode ? DarkValue : LightValue);
        }
        catch (InvalidOperationException)
        {
            // A toggle can only be clicked from a live circuit, so this is unreachable in practice; if it
            // ever happens the in-memory value still applies for this session.
        }

        return isDarkMode;
    }
}
