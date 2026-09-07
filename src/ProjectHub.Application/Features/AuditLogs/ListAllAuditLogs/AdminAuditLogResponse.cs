namespace ProjectHub.Application.Features.AuditLogs.ListAllAuditLogs;

/// <summary>
/// The READ-side shape of one row in the organisation-wide audit trail. It is a superset of
/// <c>AuditLogResponse</c>: alongside the raw record it carries the RESOLVED project name and performer
/// email, because a cross-project viewer is unreadable when every actor and project is a bare GUID.
/// </summary>
/// <remarks>
/// WHY A SEPARATE DTO INSTEAD OF EXTENDING <c>AuditLogResponse</c>?
/// The per-entity trail is rendered inside a project the caller already has open, so it needs no project
/// name, and its handler would have to pay for two extra joins to populate fields nothing displays.
/// Keeping the shapes separate means the admin view can be as rich as it needs to be while the existing
/// feature keeps its single-table projection — and no change here can regress that one.
///
/// WHY ARE THE RESOLVED NAMES NULLABLE?
/// <c>ProjectId</c> is itself nullable (the writer records it best-effort), a performer is null for changes
/// made by the system or seeder, and either referenced row may since have been soft-deleted. The UI
/// therefore treats a null name as "unknown / system" rather than assuming a value is always present.
/// </remarks>
public sealed record AdminAuditLogResponse(
    Guid Id,
    string EntityName,
    Guid EntityId,
    string Action,
    Guid? ProjectId,
    string? ProjectName,
    Guid? PerformedBy,
    string? PerformedByEmail,
    string? Changes,
    DateTime CreatedAtUtc);
