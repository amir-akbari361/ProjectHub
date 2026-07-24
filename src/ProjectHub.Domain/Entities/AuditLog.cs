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
        string? changes)
        : base(id)
    {
        EntityName = entityName;
        EntityId = entityId;
        Action = action;
        PerformedBy = performedBy;
        Changes = changes;
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

    public static AuditLog Record(
        string entityName,
        Guid entityId,
        string action,
        DateTime utcNow,
        Guid? performedBy = null,
        string? changes = null)
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
            changes);
        log.MarkCreated(utcNow, performedBy);

        return log;
    }
}
