using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.AuditLogs;

/// <summary>
/// Domain errors specific to audit log operations. Audit logs are append-only and read-only by design,
/// so this class defines only query-side failures — there are no create/update/delete errors because
/// those operations don't exist in the public API (logs are written internally by interceptors/handlers).
/// </summary>
/// <remarks>
/// WHY ARE THERE SO FEW ERRORS?
/// Audit logs have no owner authorization (any authenticated user with appropriate project membership
/// can view logs for entities they can access), no validation (they're written by the system, not users),
/// and no mutations. The only failure mode is "entity not found" when a caller asks for logs of a
/// non-existent or inaccessible parent entity.
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
}
