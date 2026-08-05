using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.AuditLogs.ListAuditLogs;

/// <summary>
/// Query to page through the immutable audit trail of ONE entity — "show me the history of this
/// project/task." A READ-side request in CQRS. It carries the target entity's name and id plus paging.
/// The caller's permission to view that entity is enforced in the handler (membership on the owning
/// project), never trusted from the client. Returns a <see cref="PagedList{T}"/> of
/// <see cref="AuditLogResponse"/> ordered newest-first.
/// </summary>
/// <remarks>
/// WHY IS <c>EntityName</c> A CALLER-SUPPLIED STRING RATHER THAN AN ENUM OR ROUTE-DERIVED TYPE?
/// Audit logs are polymorphic — any aggregate can be recorded — and the trail is keyed by the SAME
/// (EntityName, EntityId) pair the writer used. Accepting the name as a value keeps the read query as
/// generic as the storage, so adding a newly-audited entity type requires ZERO changes here. The
/// validator constrains it to a known allow-list so a caller can't probe arbitrary strings.
///
/// WHY PAGE?
/// A long-lived entity accumulates a large trail (every status change, assignment, edit). Paging keeps
/// the timeline view bounded and index-backed, mirroring every other list query in the app.
/// </remarks>
public sealed record ListAuditLogsQuery(
    string EntityName,
    Guid EntityId,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedList<AuditLogResponse>>;
