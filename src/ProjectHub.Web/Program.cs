using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using ProjectHub.Web.Client.Auth;
using ProjectHub.Web.Client.Http;
using ProjectHub.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App with the Interactive Server render mode: components run on the server over a
// SignalR circuit, so no application code ships to the browser (safer, smaller download).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Registers MudBlazor's services: the dialog/snackbar managers, the popover/theme providers, and
// the JS interop bridge. Without this call the <Mud*> components have no backing services and throw.
builder.Services.AddMudServices();

// API base URL from configuration. All typed HTTP clients below will share this base address.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException(
        "'ApiBaseUrl' is not configured. Set it in appsettings so the Web host knows where the API lives.");

// Typed HTTP clients for each API feature slice. Each gets its own HttpClient instance (from the pool)
// with the shared base address. Components inject these directly rather than using IHttpClientFactory,
// which keeps the API surface clean and the dependency graph obvious.
//
// Bridges the circuit's DI scope to the HttpClient handler pipeline. See CircuitServicesAccessor for
// the full rationale, but in short: IHttpClientFactory builds handlers in its OWN scope, so the handler
// can't inject the circuit's TokenStore directly. The accessor (populated per inbound activity by the
// circuit handler below) lets BearerTokenHandler reach the circuit-scoped TokenStore at send-time.
//
// - CircuitServicesAccessor is SCOPED so each circuit gets its own instance, but its backing store is a
//   static AsyncLocal, so the value flows into the factory-scoped handler on the same async path.
// - The CircuitHandler is registered against the framework's CircuitHandler contract; Blazor discovers
//   and runs every registered CircuitHandler for each circuit.
builder.Services.AddScoped<CircuitServicesAccessor>();
builder.Services.AddScoped<CircuitHandler, ServicesAccessorCircuitHandler>();

// WHY REGISTER BearerTokenHandler AS SCOPED?
// The handler now depends on CircuitServicesAccessor (scoped). Keeping the handler scoped keeps lifetimes
// aligned and lets each typed client get its own handler instance per circuit. The handler resolves the
// circuit-scoped TokenStore lazily at send-time via the accessor, so the token stays strictly per-user.
builder.Services.AddScoped<BearerTokenHandler>();

// AuthApiClient handles login/register/forgot, which are anonymous endpoints — no Bearer token needed.
// It goes directly to the API without the handler.
builder.Services.AddHttpClient<AuthApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));

// All other typed clients call authenticated endpoints. Each is wired through BearerTokenHandler so
// every request automatically carries the current user's JWT as Authorization: Bearer <token>.
builder.Services.AddHttpClient<ProjectsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<TasksApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<MembersApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<CommentsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<NotificationsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<SearchApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<AttachmentsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

// Authentication state: TokenStore holds the JWT/refresh tokens in memory, and the custom
// AuthenticationStateProvider reads them to determine the current user. Registered as scoped so
// each SignalR circuit gets its own isolated auth state.
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());

// Blazor's built-in auth services: AuthorizeView, CascadingAuthenticationState, [Authorize] all depend on this.
builder.Services.AddAuthorizationCore();

// WHY REGISTER AUTHENTICATION AT ALL FOR A BEARER-ONLY SPA?
// The Blazor Web App router runs a framework AUTHORIZATION step for [Authorize] pages. When a user is
// unauthorized, that step asks the authentication stack to "challenge" — and the challenge machinery
// resolves IAuthenticationService. If no authentication services are registered, resolving it throws
// (the exact "Unable to find the required 'IAuthenticationService'" error). We don't use server cookies
// or Identity — our real identity comes from the JWT decoded client-side by JwtAuthenticationStateProvider —
// but the pipeline still needs *some* authentication scheme present so the challenge has a handler.
// Registering an empty default scheme satisfies that requirement without introducing server-side sessions.
// The <RedirectToLogin> component still owns the actual "send anonymous users to /login" behavior.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "ProjectHub.Web";
        options.DefaultChallengeScheme = "ProjectHub.Web";
    })
    .AddScheme<AuthenticationSchemeOptions, NoOpAuthenticationHandler>("ProjectHub.Web", _ => { });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // HSTS tells browsers to only ever use HTTPS for this host. 30-day default; tune for prod.
    app.UseHsts();
}

app.UseHttpsRedirection();

// WHY BOTH, AND IN THIS ORDER?
// UseAuthentication populates HttpContext.User from the registered scheme (our no-op leaves it anonymous),
// and UseAuthorization runs the endpoint's authorization requirements. They must sit AFTER routing/HTTPS
// and BEFORE the component endpoints are mapped so the challenge machinery has a middleware to run in.
// Even though real identity is resolved client-side from the JWT, registering these keeps the framework's
// authorization pipeline complete and prevents the "missing IAuthenticationService" challenge failure.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// A do-nothing authentication handler. It never authenticates a request (our identity is JWT-in-the-browser,
// resolved by JwtAuthenticationStateProvider, not by a server scheme) and it never actually challenges —
// it simply returns "no result" so the framework's authorization step has a registered scheme to call
// instead of throwing for a missing IAuthenticationService. Client-side <RedirectToLogin> handles the
// user-visible redirect. This is the minimal, side-effect-free way to satisfy the pipeline.
internal sealed class NoOpAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public NoOpAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    // WHY OVERRIDE THE CHALLENGE/FORBID TO DO NOTHING?
    // The base AuthenticationHandler.HandleChallengeAsync writes a raw 401 status to the response, and
    // HandleForbiddenAsync writes a raw 403. In a server-interactive Blazor circuit that produces the
    // "HTTP ERROR 401" blank page we saw instead of the app. We don't want the HTTP layer to reject the
    // request — we want Blazor's <AuthorizeRouteView>/<NotAuthorized> to render, which our
    // <RedirectToLogin> component then turns into a client-side navigation to /login. So both handlers
    // must be true no-ops: acknowledge the challenge, touch nothing on the response, and let the
    // component tree handle the unauthorized experience.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Task.CompletedTask;

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Task.CompletedTask;
}
