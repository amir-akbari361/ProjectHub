namespace ProjectHub.Web.Client.Realtime;

/// <summary>
/// The payload the API pushes over the notification hub. This mirrors the server's <c>NotificationPush</c>
/// record field-for-field — the two are a WIRE CONTRACT, not a shared type, because the Web project does not
/// (and should not) reference the API project.
/// </summary>
/// <remarks>
/// Keep the property names and order in step with the server record. SignalR's JSON protocol matches by
/// NAME (case-insensitively) rather than position, so a rename on one side degrades to a default value
/// rather than a loud failure — which is exactly why both sides are documented as a paired contract.
///
/// <see cref="UnreadCount"/> is absolute, never a delta: a client that missed a push while offline would
/// drift forever if it incremented locally, whereas assigning an absolute value is self-healing.
/// </remarks>
public sealed record NotificationPush(
    Guid NotificationId,
    string Type,
    string Message,
    DateTime CreatedAtUtc,
    int UnreadCount);
