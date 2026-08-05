namespace ProjectHub.Application.Features.AuditLogs.ListAuditLogs;

/// <summary>
/// The READ-side shape of a single audit log entry. A flat, serialization-friendly projection that
/// carries what a history/timeline view needs: what happened (action + optional changes JSON), who did
/// it, and when. Unlike most other responses, this one does NOT hide the entity name/id because the
/// whole point of an audit trail is showing precisely which entity was affected — the caller already
/// has permission to view that parent entity or the query wouldn't have succeeded.
/// </summary>
/// <remarks>
/// WHY INCLUDE <c>EntityName</c> AND <c>EntityId</c> WHEN THE QUERY IS SCOPED TO ONE ENTITY?
/// A future enhancement might offer a cross-entity audit stream (e.g., "show me all changes across
/// this project and its tasks"), so keeping these fields in the response makes the DTO forward-compatible.
/// For a single-entity query they'll be uniform across all rows, but the client can still use them for
/// display labels like "Task ABC-123 was updated."
///
/// WHY IS <c>Changes</c> A STRING INSTEAD OF STRUCTURED JSON?
/// The domain stores it as free-form text because different actions have different shapes (a status
/// change vs. a role change vs. a delete). The read side mirrors that — it's up to the client to parse
/// if it wants structured diff rendering. Keeping it as a nullable string means the API doesn't force
/// every consumer to handle JSON deserialization even when they just want to show "Updated by Alice."
/// </remarks>
public sealed record AuditLogResponse(
    Guid Id,
    string EntityName,
    Guid EntityId,
    string Action,
    Guid? PerformedBy,
    string? Changes,
    DateTime CreatedAtUtc);
