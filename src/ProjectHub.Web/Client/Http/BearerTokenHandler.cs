using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using ProjectHub.Web.Client.Auth;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that attaches the current access token as an
/// <c>Authorization: Bearer &lt;jwt&gt;</c> header to every outgoing request made through a typed
/// <see cref="HttpClient"/> it is registered on.
/// </summary>
/// <remarks>
/// WHY A DELEGATING HANDLER INSTEAD OF SETTING THE HEADER IN EACH API CLIENT?
/// ---------------------------------------------------------------------------
/// An HttpClient message pipeline is a chain of handlers ending in the network transport. A
/// DelegatingHandler is the idiomatic, cross-cutting place to inject a concern (here: authentication)
/// that must apply to *every* call without repeating code in each method of each typed client. This is
/// the Chain-of-Responsibility pattern: each handler does its bit and calls the next. Putting the token
/// logic here keeps <see cref="ProjectsApiClient"/> and friends focused purely on endpoints and payloads
/// (Single Responsibility), and guarantees no request can accidentally be sent without the header.
///
/// WHY READ FROM TokenStore ON EVERY SEND RATHER THAN CAPTURING THE TOKEN ONCE?
/// The token rotates (login, refresh, logout). Reading the *current* value from the scoped
/// <see cref="TokenStore"/> at send-time means we always use the freshest token and immediately stop
/// sending a stale one after logout — without re-wiring any handlers.
///
/// WHY RESOLVE TokenStore FROM THE CIRCUIT SCOPE INSTEAD OF INJECTING IT DIRECTLY?
/// ------------------------------------------------------------------------------
/// IHttpClientFactory constructs the handler chain in its OWN pooled DI scope, NOT the Blazor circuit's
/// scope. A TokenStore injected straight into this constructor would therefore be a DIFFERENT instance
/// than the one the components use — with a disconnected <c>IJSRuntime</c> that can't read localStorage.
/// The result was a handler that never found a token and produced a 401 on every authenticated call,
/// even while the UI looked signed in. Instead we resolve TokenStore at send-time from the CIRCUIT'S
/// service provider, published per-activity by <see cref="ServicesAccessorCircuitHandler"/> and read via
/// <see cref="CircuitServicesAccessor"/>. That yields the same TokenStore the components use — cached
/// token, live JS interop — so the correct Bearer header is attached. See CircuitServicesAccessor for
/// the full rationale.
/// </remarks>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly CircuitServicesAccessor _circuitServicesAccessor;

    public BearerTokenHandler(CircuitServicesAccessor circuitServicesAccessor)
    {
        _circuitServicesAccessor = circuitServicesAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Resolve the CIRCUIT-scoped TokenStore (the one the components use). If we're not inside a
        // circuit activity — e.g. during pre-render before the circuit exists — Services is null and we
        // send the request unauthenticated; the framework then renders <NotAuthorized> rather than
        // firing a doomed API call.
        var tokenStore = _circuitServicesAccessor.Services?.GetService<TokenStore>();
        if (tokenStore is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // LoadAsync is cache-first: after the first successful read it returns the in-memory copy, so
        // this is cheap on every call. During pre-render (no JS yet) it returns null and we simply send
        // the request unauthenticated — the framework will render <NotAuthorized>, not hit the API.
        var tokens = await tokenStore.LoadAsync();

        // Only attach a token that exists AND has not expired. Sending an expired token would earn a
        // guaranteed 401; better to send none and let the auth-state/redirect flow take over. (Refresh-
        // token rotation is handled by the auth flow, not this low-level handler.)
        if (tokens is not null
            && !string.IsNullOrWhiteSpace(tokens.AccessToken)
            && tokens.AccessTokenExpiresAtUtc > DateTime.UtcNow)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
