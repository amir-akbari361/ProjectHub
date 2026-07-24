using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Primitives;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; private init; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void MarkCreated(DateTime occurredAtUtc, Guid? createdBy = null)
    {
        CreatedAtUtc = occurredAtUtc;
        CreatedBy = createdBy;
    }

    protected void MarkUpdated(DateTime occurredAtUtc, Guid? updatedBy = null)
    {
        UpdatedAtUtc = occurredAtUtc;
        UpdatedBy = updatedBy;
    }

    protected void MarkDeleted(DateTime occurredAtUtc, Guid? deletedBy = null)
    {
        IsDeleted = true;
        DeletedAtUtc = occurredAtUtc;
        UpdatedAtUtc = occurredAtUtc;
        UpdatedBy = deletedBy;
    }

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}