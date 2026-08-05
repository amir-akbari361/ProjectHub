using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.Notifications.ListNotifications;

/// <summary>
/// Query to page through the AUTHENTICATED caller's own notification inbox. READ side of CQRS. Carries
/// only paging inputs plus an <see cref="UnreadOnly"/> toggle — it deliberately has NO recipient id,
/// because the recipient is always the current user (resolved in the handler from the token). Returns a
/// <see cref="PagedList{T}"/> of <see cref="NotificationResponse"/>.
/// </summary>
/// <remarks>
/// WHY NO RECIPIENT ON THE QUERY?
/// Accepting a recipient id from the client would invite an IDOR (Insecure Direct Object Reference): a
/// caller could ask for someone else's inbox. By binding the recipient to the authenticated principal in
/// the handler, the API is secure by construction — there is no parameter to tamper with.
///
/// WHY AN <c>UnreadOnly</c> FLAG INSTEAD OF A SEPARATE ENDPOINT?
/// "Unread only" is a filter over the SAME read model, not a different resource. A boolean keeps one
/// query, one handler, one SQL shape — the badge count view (unread) and the full inbox view differ only
/// by a WHERE clause.
/// </remarks>
public sealed record ListNotificationsQuery(
    bool UnreadOnly = false,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedList<NotificationResponse>>;
