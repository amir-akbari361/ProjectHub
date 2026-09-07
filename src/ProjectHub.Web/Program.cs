using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using ProjectHub.Web.Client.Auth;
using ProjectHub.Web.Client.Http;
using ProjectHub.Web.Client.Realtime;
using ProjectHub.Web.Client.State;
using ProjectHub.Web.Client.Theme;
using ProjectHub.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App with the Interactive Server render mode: components run on the server over a SignalR
// circuit, so no application code ships to the browser (safer, and a smaller download).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Registers MudBlazor's services: the dialog/snackbar managers, the popover/theme providers, and the JS
// interop bridge. Without this call the <Mud*> components have no backing services and throw.
builder.Services.AddMudServices();

// API base URL from configuration. Every typed client below shares it.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException(
        "'ApiBaseUrl' is not configured. Set it in appsettings so the Web host knows where the API lives.");

// -------------------------------------------------------------------------------------------------
// CIRCUIT ↔ HTTP HANDLER BRIDGE
//
// IHttpClientFactory builds handler chains in its OWN pooled DI scope, NOT the Blazor circuit's. A handler
// that injected the circuit's TokenStore directly would therefore get a DIFFERENT instance, with a
// disconnected IJSRuntime that cannot read localStorage — the cause of "401 on every call while the UI looks
// signed in". CircuitServicesAccessor publishes the circuit's service provider on an AsyncLocal, so
// BearerTokenHandler can resolve the RIGHT services at send-time. See those two types for the full rationale.
// -------------------------------------------------------------------------------------------------
builder.Services.AddScoped<CircuitServicesAccessor>();
builder.Services.AddScoped<CircuitHandler, ServicesAccessorCircuitHandler>();

// Scoped so each circuit gets its own handler instance and lifetimes stay aligned with the accessor it depends
// on. The handler resolves the token provider lazily at send-time, so tokens stay strictly per-user.
builder.Services.AddScoped<BearerTokenHandler>();

// -------------------------------------------------------------------------------------------------
// TYPED HTTP CLIENTS — one per API feature slice.
//
// AuthApiClient is registered WITHOUT the bearer handler for two reasons: its endpoints are anonymous, and its
// RefreshAsync is what the handler CALLS when a token has expired — routing it back through the handler would
// be an infinite cycle (refresh → handler → needs a token → refresh).
//
// Every other client goes through BearerTokenHandler, which attaches (and silently renews) the access token.
// The local helper exists so adding a client is one line and cannot accidentally omit authentication — the
// failure mode of the previous copy-pasted block, where a new client's missing .AddHttpMessageHandler would
// present as sporadic 401s rather than a compile error.
// -------------------------------------------------------------------------------------------------
builder.Services.AddHttpClient<AuthApiClient>(ConfigureApiClient);

AddAuthenticatedApiClient<ProjectsApiClient>();
AddAuthenticatedApiClient<TasksApiClient>();
AddAuthenticatedApiClient<SprintsApiClient>();
AddAuthenticatedApiClient<MembersApiClient>();
AddAuthenticatedApiClient<CommentsApiClient>();
AddAuthenticatedApiClient<AttachmentsApiClient>();
AddAuthenticatedApiClient<NotificationsApiClient>();
AddAuthenticatedApiClient<SearchApiClient>();
AddAuthenticatedApiClient<AuditLogsApiClient>();

// Admin-only surface. Registered like any other client — the API's "Admin" policy, not this registration, is
// what restricts it, so a non-admin circuit simply receives 403s from every call.
AddAuthenticatedApiClient<AdminUsersApiClient>();

// -------------------------------------------------------------------------------------------------
// AUTHENTICATION STATE
//
// TokenStore persists the token pair in localStorage; AccessTokenProvider decides whether the access half is
// still usable and performs the silent refresh; JwtAuthenticationStateProvider turns the stored token into the
// ClaimsPrincipal that <AuthorizeView> and the router read. All scoped, so each circuit is isolated.
// -------------------------------------------------------------------------------------------------
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AccessTokenProvider>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();

// Registered against BOTH its concrete type (above, for the flows that need to push a state change) and the
// framework's abstraction (below, for everything that just reads state). Resolving the same instance for both is
// essential: two instances would mean a login notified subscribers of one while components read the other.
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());

// Blazor's built-in authorization services: AuthorizeView, CascadingAuthenticationState and [Authorize] all
// depend on these.
builder.Services.AddAuthorizationCore();

// -------------------------------------------------------------------------------------------------
// UI STATE — cross-component state that cannot live in a single component.
// -------------------------------------------------------------------------------------------------
builder.Services.AddScoped<NotificationState>();
builder.Services.AddScoped<ThemePreferenceStore>();

// The live channel that feeds NotificationState. Scoped == one connection per circuit, which is the right
// grain: the connection is authenticated as ONE user, and the circuit's own scope disposes it (the class is
// IAsyncDisposable) when the user closes the tab. MainLayout starts it once the user is known.
builder.Services.AddScoped<NotificationHubClient>();

// WHY REGISTER AUTHENTICATION AT ALL FOR A BEARER-ONLY SPA?
// The Blazor router runs a framework AUTHORIZATION step for [Authorize] pages. When a user is unauthorized that
// step asks the authentication stack to "challenge", and the challenge machinery resolves IAuthenticationService
// — which throws if no authentication services are registered ("Unable to find the required
// 'IAuthenticationService'"). We use neither cookies nor server-side Identity (our identity is the JWT, decoded
// client-side), but the pipeline still needs SOME scheme present so the challenge has a handler. An empty default
// scheme satisfies that without introducing server-side sessions; <RedirectToLogin> owns the actual redirect.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = NoOpAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = NoOpAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, NoOpAuthenticationHandler>(
        NoOpAuthenticationHandler.SchemeName, _ => { });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    // HSTS tells browsers to only ever reach this host over HTTPS. 30-day default; tune before production.
    app.UseHsts();
}

app.UseHttpsRedirection();

// ORDER MATTERS. UseAuthentication populates HttpContext.User from the registered scheme (our no-op leaves it
// anonymous) and UseAuthorization evaluates the endpoint's requirements. Both must sit AFTER routing/HTTPS and
// BEFORE the component endpoints are mapped, so the challenge machinery has middleware to run in. Even though
// real identity is resolved client-side, registering these keeps the framework's pipeline complete and prevents
// the missing-IAuthenticationService failure described above.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

// -------------------------------------------------------------------------------------------------
// WHY .AllowAnonymous() ON THE COMPONENT ENDPOINTS — THIS IS LOAD-BEARING, NOT A LOOSENING
//
// MapRazorComponents promotes each page component's [Authorize] attribute into ENDPOINT authorization
// metadata, so ASP.NET evaluates it in UseAuthorization before the component ever renders. That model assumes
// server-side identity (cookies/Identity). Ours is a Bearer JWT held in the browser's localStorage, which the
// server cannot see: HttpContext.User is ALWAYS anonymous here, by design.
//
// So every [Authorize] page failed authorization at the HTTP level and issued a challenge. NoOpAuthenticationHandler
// answers a challenge by touching nothing (deliberately — writing a bare 401 renders a blank "HTTP ERROR 401" page
// in a circuit), and the net result was a 200 response with ZERO BYTES of HTML. That is exactly the reported
// symptom: "/" and every other page came back as an empty white page, while /login — the one page without
// [Authorize] — worked, so the app appeared to require typing /login by hand.
//
// Opting the component endpoints out of HTTP-level authorization is the correct fix because that check cannot
// ever be meaningful in this architecture. Authorization is NOT weakened:
//   • The CLIENT still enforces it. <AuthorizeRouteView> reads the same [Authorize] attribute off the routed
//     component and renders <NotAuthorized>/<RedirectToLogin> for an anonymous visitor.
//   • The API still enforces it. Every endpoint that returns real data validates the JWT's signature itself, so
//     a visitor who forced a page to render would see an empty shell and a wall of 401s — no data.
// The page markup this now serves is a UI shell containing no user data, which is precisely what it was already
// designed to be for the pre-authentication window.
// -------------------------------------------------------------------------------------------------
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.Run();

// -------------------------------------------------------------------------------------------------
// Local helpers. Declared after the pipeline because top-level statements require it; they are hoisted, so the
// registrations above can call them.
// -------------------------------------------------------------------------------------------------

void ConfigureApiClient(HttpClient client) => client.BaseAddress = new Uri(apiBaseUrl);

void AddAuthenticatedApiClient<TClient>() where TClient : class =>
    builder.Services
        .AddHttpClient<TClient>(ConfigureApiClient)
        .AddHttpMessageHandler<BearerTokenHandler>();

/// <summary>
/// An authentication handler that never authenticates and never challenges. Our identity is a JWT held in the
/// browser and decoded by <see cref="JwtAuthenticationStateProvider"/>, not a server scheme; this type exists
/// only so the framework's authorization step has a registered scheme to call instead of throwing for a missing
/// <c>IAuthenticationService</c>.
/// </summary>
/// <remarks>
/// WHY THE CHALLENGE AND FORBID OVERRIDES MUST BE NO-OPS
/// The base implementations write a bare 401 and 403 to the response. Inside a server-interactive circuit that
/// produces a blank "HTTP ERROR 401" page instead of the app. We do not want the HTTP layer to reject the
/// request at all — we want Blazor's <c>&lt;AuthorizeRouteView&gt;/&lt;NotAuthorized&gt;</c> to render, which
/// <c>&lt;RedirectToLogin&gt;</c> then turns into a client-side navigation. So both handlers acknowledge the
/// challenge, touch nothing on the response, and let the component tree own the unauthorized experience.
/// </remarks>
internal sealed class NoOpAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The scheme name, referenced by both the default-scheme options and the registration.</summary>
    public const string SchemeName = "ProjectHub.Web";

    public NoOpAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Task.CompletedTask;

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Task.CompletedTask;
}
