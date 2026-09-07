using Microsoft.AspNetCore.SignalR.Client;
using ProjectHub.Web.Client.Auth;
using ProjectHub.Web.Client.State;

namespace ProjectHub.Web.Client.Realtime;

/// <summary>
/// Holds this circuit's live connection to the API's notification hub, so a new notification reaches the UI
/// the moment it is created instead of on the user's next navigation. It owns the connection lifecycle
/// (connect, automatic reconnect, resync, dispose), updates <see cref="NotificationState"/> for the badge,
/// and raises <see cref="Received"/> so the layout can show a toast.
/// </summary>
/// <remarks>
/// WHY THE WEB HOST CONNECTS, NOT THE BROWSER
/// This is an Interactive Server app: the component tree lives on the Web host, so that is where a push has
/// to land to trigger a re-render. The connection is therefore server-to-server (Web host → API), which also
/// means the JWT never has to be exposed to a browser-side hub connection and no CORS policy is involved.
///
/// WHY THIS PUBLISHES THE CIRCUIT SCOPE AROUND ITS OWN CALLBACKS  ← the subtle part
/// <see cref="CircuitServicesAccessor"/> is populated by <see cref="ServicesAccessorCircuitHandler"/> only
/// for the duration of an INBOUND circuit activity (a UI event, a JS interop callback). A SignalR callback
/// on this connection is not inbound circuit activity — it arrives on a thread-pool thread with no such
/// window open. Any HTTP call made from there would find a null accessor in
/// <see cref="ProjectHub.Web.Client.Http.BearerTokenHandler"/>, which by design then sends the request
/// UNAUTHENTICATED: the resync below would 401 and be swallowed, leaving the badge permanently stale with
/// nothing in the logs to explain it. Because this service is itself circuit-scoped, it can hold the
/// circuit's own provider and publish it for the duration of its background work — the same rendezvous the
/// circuit handler performs, applied to a flow Blazor does not classify as inbound.
///
/// WHY FAILING TO CONNECT IS NOT AN ERROR
/// Real-time delivery is an enhancement over a durable inbox. If the API is not up when the circuit starts,
/// or the socket is lost for good, the layout's existing on-navigation refresh still keeps the badge
/// correct — so a connection failure is logged and the app carries on degraded rather than broken.
/// </remarks>
public sealed class NotificationHubClient : IAsyncDisposable
{
    private readonly NotificationState _notifications;
    private readonly AccessTokenProvider _accessTokenProvider;
    private readonly CircuitServicesAccessor _circuitServicesAccessor;
    private readonly IServiceProvider _circuitServices;
    private readonly ILogger<NotificationHubClient> _logger;
    private readonly Uri _hubUri;

    // Serialises Start so two near-simultaneous callers (a re-render racing the first render) cannot each
    // build a connection and leave one of them orphaned and undisposed.
    private readonly SemaphoreSlim _startGate = new(1, 1);

    private HubConnection? _connection;

    public NotificationHubClient(
        NotificationState notifications,
        AccessTokenProvider accessTokenProvider,
        CircuitServicesAccessor circuitServicesAccessor,
        IServiceProvider circuitServices,
        IConfiguration configuration,
        ILogger<NotificationHubClient> logger)
    {
        _notifications = notifications;
        _accessTokenProvider = accessTokenProvider;
        _circuitServicesAccessor = circuitServicesAccessor;
        _circuitServices = circuitServices;
        _logger = logger;

        var apiBaseUrl = configuration["ApiBaseUrl"]
            ?? throw new InvalidOperationException(
                "'ApiBaseUrl' is not configured. Set it in appsettings so the Web host knows where the API lives.");

        // Normalising the trailing slash before combining means the hub path resolves correctly whether or not
        // the configured base URL ends in one — a missing slash would otherwise silently drop the last segment.
        _hubUri = new Uri(new Uri(apiBaseUrl.TrimEnd('/') + "/"), "hubs/notifications");
    }

    /// <summary>
    /// Raised for each notification pushed to this user. The badge is already updated by the time this fires;
    /// subscribers exist to present the message (a toast). Handlers run on a NON-UI thread, so a subscriber
    /// that touches component state must marshal through <c>InvokeAsync</c>.
    /// </summary>
    public event Action<NotificationPush>? Received;

    /// <summary>
    /// Opens the connection if it is not already open. Safe to call more than once — repeat calls are no-ops.
    /// Never throws: a failure to connect degrades to the non-real-time experience.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return;
        }

        await _startGate.WaitAsync(cancellationToken);

        try
        {
            if (_connection is not null)
            {
                return;
            }

            var connection = new HubConnectionBuilder()
                .WithUrl(_hubUri, options =>
                {
                    // Invoked on every (re)connect, so an expiring token is renewed for the life of the
                    // circuit rather than only at first handshake. TokenStore caches in memory after its
                    // first read, so this normally resolves without touching JS interop — which matters
                    // because reconnects happen off the circuit's synchronisation context.
                    options.AccessTokenProvider = () => _accessTokenProvider.GetAccessTokenAsync();
                })
                .WithAutomaticReconnect()
                .Build();

            connection.On<NotificationPush>(NotificationReceivedMethod, OnNotificationReceived);
            connection.Reconnected += OnReconnectedAsync;
            connection.Closed += OnClosedAsync;

            await connection.StartAsync(cancellationToken);

            _connection = connection;

            _logger.LogInformation(
                "Notification hub connected to {HubUri} (connection {ConnectionId}).",
                _hubUri, connection.ConnectionId);
        }
        catch (Exception exception)
        {
            // WithAutomaticReconnect only covers a connection that was once established, so there is nothing
            // to retry here. The layout's on-navigation refresh remains the fallback for badge accuracy.
            _logger.LogWarning(
                exception,
                "Could not connect to the notification hub at {HubUri}; notifications will update on navigation instead.",
                _hubUri);

            await DisposeConnectionAsync();
        }
        finally
        {
            _startGate.Release();
        }
    }

    private void OnNotificationReceived(NotificationPush push)
    {
        // Absolute count from the server, so this cannot drift even if an earlier push was missed.
        // SetUnreadCount raises NotificationState.Changed, which the layout marshals onto the UI thread.
        _notifications.SetUnreadCount(push.UnreadCount);

        _logger.LogDebug(
            "Received notification {NotificationId} ({Type}); unread is now {UnreadCount}.",
            push.NotificationId, push.Type, push.UnreadCount);

        Received?.Invoke(push);
    }

    private async Task OnReconnectedAsync(string? connectionId)
    {
        // A push delivered while the socket was down is gone — the hub does not replay. Re-reading the count
        // from the API is what stops a dropped connection from leaving a permanently wrong badge.
        _logger.LogInformation(
            "Notification hub reconnected (connection {ConnectionId}); resyncing the unread count.",
            connectionId);

        await WithCircuitServicesAsync(_notifications.RefreshAsync);
    }

    private Task OnClosedAsync(Exception? exception)
    {
        if (exception is null)
        {
            _logger.LogInformation("Notification hub connection closed.");
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Notification hub connection closed after reconnection attempts were exhausted; "
                + "the badge will update on navigation.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs circuit-dependent work with this circuit's service provider published on
    /// <see cref="CircuitServicesAccessor"/>, so services resolved at send-time (notably the bearer token
    /// provider behind every typed API client) find the RIGHT scope. See the class remarks for why a hub
    /// callback would otherwise silently run unauthenticated.
    /// </summary>
    private async Task WithCircuitServicesAsync(Func<Task> work)
    {
        _circuitServicesAccessor.Services = _circuitServices;

        try
        {
            await work();
        }
        catch (Exception exception)
        {
            // Never let a resync failure bubble into SignalR's reconnect machinery, which would tear the
            // connection down for a merely stale number.
            _logger.LogWarning(exception, "Failed to resync notifications after reconnecting.");
        }
        finally
        {
            // Cleared so a disposed scope can never be observed by later work on this execution context.
            _circuitServicesAccessor.Services = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync();
        _startGate.Dispose();
    }

    private async Task DisposeConnectionAsync()
    {
        if (_connection is null)
        {
            return;
        }

        // Detach first: DisposeAsync completes pending callbacks, and a handler firing into a half-disposed
        // circuit is the classic source of "collection was modified"/ObjectDisposed noise at shutdown.
        _connection.Reconnected -= OnReconnectedAsync;
        _connection.Closed -= OnClosedAsync;

        try
        {
            await _connection.DisposeAsync();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Ignoring a fault while disposing the notification hub connection.");
        }
        finally
        {
            _connection = null;
        }
    }

    /// <summary>
    /// The hub method name the API invokes. Must match <c>NotificationHub.NotificationReceived</c> on the
    /// server — a mismatch fails silently at runtime, so both sides name it in one place.
    /// </summary>
    private const string NotificationReceivedMethod = "NotificationReceived";
}
