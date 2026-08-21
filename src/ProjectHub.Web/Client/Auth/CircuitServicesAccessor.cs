using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ProjectHub.Web.Client.Auth;

/// <summary>
/// Exposes the CURRENT circuit's DI scope to code that runs outside the component tree — most
/// importantly the <see cref="System.Net.Http.HttpClient"/> message-handler pipeline.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS (the bug it fixes)
/// ----------------------------------
/// In Blazor Server, <c>IHttpClientFactory</c> builds and POOLS the <c>DelegatingHandler</c> chain in a
/// SEPARATE, factory-owned DI scope — deliberately, because handlers are reused across requests for the
/// handler-lifetime window. That means any scoped service a handler resolves (e.g. our
/// <see cref="TokenStore"/>, and the <c>IJSRuntime</c> it depends on) is NOT the circuit's instance.
/// A circuit-bound <c>IJSRuntime</c> resolved from the wrong scope isn't connected, so localStorage
/// interop throws and the handler ends up with no token — every authenticated API call then gets a 401,
/// even though the same user's auth state (read from the circuit-scoped TokenStore) looks signed in.
///
/// THE FIX (Microsoft's documented pattern)
/// ----------------------------------------
/// A <see cref="CircuitHandler"/> runs INSIDE the circuit scope. By hooking its inbound-activity pipeline
/// we can stash the circuit's <see cref="IServiceProvider"/> into a static <see cref="AsyncLocal{T}"/>
/// for the duration of that activity. Because the component's async work (OnInitializedAsync → typed
/// client → BearerTokenHandler.SendAsync) flows on the same execution context, the handler can read this
/// AsyncLocal and resolve the REAL circuit-scoped TokenStore — the one with the cached token and a live
/// JS-interop channel. The value is cleared when the activity completes so nothing leaks between circuits.
///
/// WHY A STATIC AsyncLocal (not an instance field)?
/// The handler resolves its own <see cref="CircuitServicesAccessor"/> from the factory scope, which is a
/// DIFFERENT instance than the one the circuit uses. A static backing field is shared across every
/// instance in the process, while <see cref="AsyncLocal{T}"/> still keeps the VALUE isolated per async
/// flow (per circuit activity). That combination is exactly what lets the two scopes rendezvous safely.
/// </remarks>
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> CircuitServices = new();

    /// <summary>The circuit's service provider while an inbound circuit activity is executing; otherwise null.</summary>
    public IServiceProvider? Services
    {
        get => CircuitServices.Value;
        set => CircuitServices.Value = value;
    }
}

/// <summary>
/// A <see cref="CircuitHandler"/> that publishes the circuit's <see cref="IServiceProvider"/> to
/// <see cref="CircuitServicesAccessor"/> for the lifetime of each inbound activity, then clears it.
/// </summary>
/// <remarks>
/// Registered as a scoped <see cref="CircuitHandler"/>, so the injected <see cref="IServiceProvider"/> IS
/// the circuit scope. <see cref="CircuitHandler.CreateInboundActivityHandler"/> (available since .NET 8)
/// wraps every inbound circuit action — including component lifecycle methods where our API calls
/// originate — which is precisely the window during which the handler needs the circuit's TokenStore.
/// </remarks>
internal sealed class ServicesAccessorCircuitHandler : CircuitHandler
{
    private readonly IServiceProvider _circuitServices;
    private readonly CircuitServicesAccessor _accessor;

    public ServicesAccessorCircuitHandler(IServiceProvider circuitServices, CircuitServicesAccessor accessor)
    {
        _circuitServices = circuitServices;
        _accessor = accessor;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            _accessor.Services = _circuitServices;
            try
            {
                await next(context);
            }
            finally
            {
                // Always clear, even if the activity threw, so a disposed scope is never observed later.
                _accessor.Services = null;
            }
        };
}
