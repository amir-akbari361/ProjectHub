using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.AuditLogs;

/// <summary>
/// Domain errors specific to audit log operations. Audit logs are append-only and read-only by design,
/// so this class defines only query-side failures — there are no create/update/delete errors because
/// those operations don't exist in the public API (logs are written internally by interceptors/handlers).
/// </summary>
/// <remarks>
/// WHY ARE THERE SO FEW ERRORS?
/// Audit logs are read-scoped by project membership (or the Admin role) rather than by a per-row owner,
/// have no validation (they're written by the system, not users), and no mutations. The only failure
/// mode is "entity not found" — returned both when the parent entity doesn't exist and when the caller
/// lacks access, so the two are indistinguishable to a client.
/// </remarks>
public static class AuditLogErrors
{
    /// <summary>
    /// Returned when a caller asks for audit logs of an entity (project/task/etc.) that doesn't exist
    /// or that they lack permission to view. The entity type and id are embedded in the message so the
    /// client can show context, e.g., "No audit history found for task {id}" without leaking whether
    /// the task exists vs. is merely inaccessible.
    /// </summary>
    public static Error EntityNotFound(string entityName, Guid entityId) =>
        Error.NotFound(
            "AuditLog.EntityNotFound",
            $"No audit trail found for {entityName} with id '{entityId}'. The entity may not exist or you may lack access to it.");

    /// <summary>
    /// Returned when a non-Admin reaches the organisation-wide audit viewer. Unlike
    /// <see cref="EntityNotFound"/> this is a deliberate 403 rather than a 404: the endpoint's existence is
    /// not a secret (it is a documented admin feature), and there is no specific entity whose existence
    /// could leak. Telling the caller plainly that the feature is Admin-only is more useful than pretending
    /// the route does not exist.
    /// </summary>
    public static readonly Error AdminOnly = Error.Forbidden(
        "AuditLog.AdminOnly",
        "Only administrators can view the organisation-wide audit trail.");
}
