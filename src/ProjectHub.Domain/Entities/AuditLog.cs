using ProjectHub.Domain.Common;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Domain.Entities;

public sealed class AuditLog : AggregateRoot
{
    private AuditLog(
        Guid id,
        string entityName,
        Guid entityId,
        string action,
        Guid? performedBy,
        string? changes,
        Guid? projectId)
        : base(id)
    {
        EntityName = entityName;
        EntityId = entityId;
        Action = action;
        PerformedBy = performedBy;
        Changes = changes;
        ProjectId = projectId;
    }

    private AuditLog()
        : base(Guid.Empty)
    {
        EntityName = null!;
        Action = null!;
    }

    public string EntityName { get; private set; }

    public Guid EntityId { get; private set; }

    public string Action { get; private set; }

    public Guid? PerformedBy { get; private set; }

    public string? Changes { get; private set; }

    /// <summary>
    /// The project this change belongs to, denormalised onto the row so both membership-scoped reads
    /// and the admin-wide viewer can filter by project with a single indexed predicate instead of
    /// resolving the owning project per entity type at query time. Nullable: it is best-effort for
    /// entities whose owning project cannot be resolved from the change tracker at write time (a
    /// comment or attachment whose parent task is not tracked in the same unit of work).
    /// </summary>
    public Guid? ProjectId { get; private set; }

    public static AuditLog Record(
        string entityName,
        Guid entityId,
        string action,
        DateTime utcNow,
        Guid? performedBy = null,
        string? changes = null,
        Guid? projectId = null)
    {
        var normalizedEntity = Guard.NotNullOrWhiteSpace(entityName, nameof(entityName)).Trim();
        var normalizedAction = Guard.NotNullOrWhiteSpace(action, nameof(action)).Trim();
        Guard.NotEmpty(entityId, nameof(entityId));

        var log = new AuditLog(
            Guid.NewGuid(),
            normalizedEntity,
            entityId,
            normalizedAction,
            performedBy,
            changes,
            projectId);
        log.MarkCreated(utcNow, performedBy);

        return log;
    }
}
