using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Notifications;

/// <summary>
/// The single catalog of modeled failures for the Notifications feature. Centralizing every
/// <see cref="Error"/> a notification handler can return keeps codes stable and message wording
/// consistent (DRY), and gives the API a single, machine-readable contract to branch on.
/// </summary>
/// <remarks>
/// WHY NO "UNAUTHENTICATED" HERE?
/// The 401 case is produced inline by each handler with <see cref="Error.Unauthorized"/> because it is
/// identical across the whole app and carries no feature-specific wording. This catalog holds only the
/// failures that are SPECIFIC to notifications.
///
/// WHY DOES "NotFound" TAKE AN ID?
/// A recipient can only ever act on their OWN notifications. "Belongs to someone else" and "does not
/// exist" therefore collapse into the SAME 404 — we never reveal that a notification exists but belongs
/// to another user (no information disclosure).
/// </remarks>
public static class NotificationErrors
{
    public static Error NotFound(Guid notificationId) => Error.NotFound(
        "Notifications.NotFound",
        $"Notification '{notificationId}' was not found.");
}
