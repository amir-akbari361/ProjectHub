using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Domain.Primitives;

public abstract class AggregateRoot : Entity, IAggregateRoot
{
    protected AggregateRoot(Guid id)
        : base(id)
    {
    }
}