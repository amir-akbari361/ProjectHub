using ProjectHub.Application.Abstractions.Messaging;
using ProjectHub.Application.Common;

namespace ProjectHub.Application.Features.AuditLogs.ListAllAuditLogs;

/// <summary>
/// Query for the ORGANISATION-WIDE audit trail — "show me everything that changed, anywhere." Admin-only,
/// paged, newest-first. Every filter is optional, so a bare query returns the most recent activity across
/// all projects; supplying filters narrows it without changing the shape of the result.
/// </summary>
/// <remarks>
/// HOW THIS DIFFERS FROM <c>ListAuditLogsQuery</c>
/// That query answers "the history OF this entity" and is scoped by project membership. This one answers
/// "what has been happening" and is scoped by ROLE instead: it deliberately crosses project boundaries,
/// which is precisely why it is restricted to Admins. Keeping them as two queries rather than overloading
/// one with an "all" mode means the membership-scoped path can never accidentally return another project's
/// rows because a parameter was omitted.
///
/// WHY FILTER BY <c>ProjectId</c> RATHER THAN RESOLVING THE ENTITY?
/// The denormalised <c>AuditLog.ProjectId</c> makes this a single indexed predicate. The per-entity query
/// cannot use it (it must resolve the live entity so a project with no trail yet still authorises), but a
/// global viewer is filtering rather than authorising, so the column is exactly the right tool here.
///
/// WHY IS THE DATE RANGE INCLUSIVE-FROM / INCLUSIVE-TO?
/// The UI presents it as two date pickers, and a user choosing "27 August" to "27 August" means "that day".
/// The caller is expected to pass an end-of-day value for <see cref="ToUtc"/>; the handler applies both
/// bounds inclusively so neither endpoint silently drops rows.
/// </remarks>
public sealed record ListAllAuditLogsQuery(
    string? EntityName = null,
    Guid? ProjectId = null,
    Guid? PerformedBy = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedList<AdminAuditLogResponse>>;
