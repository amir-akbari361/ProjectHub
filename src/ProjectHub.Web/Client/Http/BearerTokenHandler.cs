using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using ProjectHub.Web.Client.Auth;

namespace ProjectHub.Web.Client.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that attaches the current access token as
/// <c>Authorization: Bearer &lt;jwt&gt;</c> to every request made through a typed client it is registered on.
/// </summary>
/// <remarks>
/// WHY A DELEGATING HANDLER INSTEAD OF SETTING THE HEADER IN EACH API CLIENT?
/// An HttpClient pipeline is a chain of handlers ending in the network transport, and a DelegatingHandler is the
/// idiomatic place for a cross-cutting concern that must apply to EVERY call. This is Chain of Responsibility:
/// each handler does its bit and defers to the next. Keeping authentication here leaves
/// <see cref="ProjectsApiClient"/> and friends focused purely on endpoints and payloads (Single Responsibility),
/// and makes it structurally impossible to forget the header on a new endpoint.
///
/// WHY IT ASKS <see cref="AccessTokenProvider"/> RATHER THAN READING THE TOKEN ITSELF
/// The token expires. This handler used to read <c>TokenStore</c> directly and simply omit an expired token,
/// which meant the first call after the access-token lifetime elapsed silently lost its credentials, 401'd, and
/// bounced the user to /login in the middle of their work — even though a perfectly valid refresh token was
/// sitting in storage. Delegating to the provider means an expiring token is RENEWED here, transparently, and
/// this class keeps its single job of attaching a header.
///
/// WHY THE PROVIDER IS RESOLVED FROM THE CIRCUIT SCOPE AT SEND-TIME
/// IHttpClientFactory builds the handler chain in its OWN pooled DI scope, not the Blazor circuit's. Anything
/// injected straight into this constructor would therefore be a DIFFERENT instance from the one the components
/// use — with a disconnected IJSRuntime that cannot read localStorage. That produced a handler which never found
/// a token and 401'd every authenticated call while the UI looked signed in. Instead we reach into the CIRCUIT'S
/// service provider, published per inbound activity by <see cref="ServicesAccessorCircuitHandler"/> and read via
/// <see cref="CircuitServicesAccessor"/>, so we get the same TokenStore the components share.
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
        // Outside a circuit activity — e.g. during pre-render, before the circuit exists — there is no user and
        // therefore no token. Send unauthenticated and let the framework render <NotAuthorized>.
        var tokenProvider = _circuitServicesAccessor.Services?.GetService<AccessTokenProvider>();

        if (tokenProvider is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var accessToken = await tokenProvider.GetAccessTokenAsync();

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
