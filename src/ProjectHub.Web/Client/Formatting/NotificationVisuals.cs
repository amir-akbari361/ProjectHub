using MudBlazor;
using ProjectHub.Domain.Enums;

namespace ProjectHub.Web.Client.Formatting;

/// <summary>
/// Maps a <see cref="NotificationType"/> to the icon and accent colour that represent it in the UI.
/// </summary>
/// <remarks>
/// WHY THIS IS A SHARED HELPER AND NOT PRIVATE TO A COMPONENT.
/// The same notification is drawn in two places — the full inbox at <c>/notifications</c> and the app-bar
/// dropdown — and a reader learns the shapes: a swap-arrows icon MEANS a status change wherever it appears.
/// If each component carried its own <c>switch</c>, adding a new <see cref="NotificationType"/> to one and
/// forgetting the other would give the same event two different faces. One function is the single source of
/// truth for "what does this kind of notification look like", exactly as <see cref="DisplayText"/> is for
/// "what does this value read as".
/// </remarks>
public static class NotificationVisuals
{
    /// <summary>A representative icon, so a feed scans by shape and not by reading every line.</summary>
    public static string IconFor(NotificationType type) => type switch
    {
        NotificationType.TaskAssigned => Icons.Material.Filled.AssignmentInd,
        NotificationType.TaskStatusChanged => Icons.Material.Filled.SwapHoriz,
        NotificationType.CommentAdded => Icons.Material.Filled.Comment,
        NotificationType.MentionedInComment => Icons.Material.Filled.AlternateEmail,
        NotificationType.ProjectInvitation => Icons.Material.Filled.GroupAdd,
        NotificationType.SprintStarted => Icons.Material.Filled.PlayCircleOutline,
        _ => Icons.Material.Filled.Notifications
    };

    /// <summary>
    /// The accent colour for an UNREAD notification of this kind. A direct mention is the one type that
    /// warrants a warning tint — it is addressed to you personally and usually wants a reply; an invitation
    /// gets the secondary accent; everything else takes the primary brand colour. (A read notification is
    /// drawn muted by the caller regardless, so this only governs the unread state.)
    /// </summary>
    public static Color ColorFor(NotificationType type) => type switch
    {
        NotificationType.MentionedInComment => Color.Warning,
        NotificationType.ProjectInvitation => Color.Secondary,
        _ => Color.Primary
    };
}
