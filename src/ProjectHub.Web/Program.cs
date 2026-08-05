using Microsoft.AspNetCore.Components.Authorization;
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
builder.Services.AddHttpClient<AuthApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<ProjectsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<TasksApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<MembersApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<CommentsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<NotificationsApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<SearchApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));

// Authentication state: TokenStore holds the JWT/refresh tokens in memory, and the custom
// AuthenticationStateProvider reads them to determine the current user. Registered as scoped so
// each SignalR circuit gets its own isolated auth state.
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());

// Blazor's built-in auth services: AuthorizeView, CascadingAuthenticationState, [Authorize] all depend on this.
builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // HSTS tells browsers to only ever use HTTPS for this host. 30-day default; tune for prod.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
