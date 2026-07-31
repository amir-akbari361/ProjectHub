using MudBlazor.Services;
using ProjectHub.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App with the Interactive Server render mode: components run on the server over a
// SignalR circuit, so no application code ships to the browser (safer, smaller download).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Registers MudBlazor's services: the dialog/snackbar managers, the popover/theme providers, and
// the JS interop bridge. Without this call the <Mud*> components have no backing services and throw.
builder.Services.AddMudServices();

// Typed HttpClient for talking to the ProjectHub API. Named "ProjectHubApi" and given a BaseAddress
// from configuration so the URL differs per environment without code changes. Using the factory
// (not a hand-newed HttpClient) gives us pooled, correctly-disposed handlers and avoids socket
// exhaustion. Feature slices will inject IHttpClientFactory (or typed clients) built on this.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException(
        "'ApiBaseUrl' is not configured. Set it in appsettings so the Web host knows where the API lives.");

builder.Services.AddHttpClient("ProjectHubApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

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
