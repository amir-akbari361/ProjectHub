using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Application.Abstractions.Persistence;

/// <summary>
/// Generic write-side repository. Only <see cref="IAggregateRoot"/> types are eligible
/// — Evans' rule from DDD: consistency boundaries are aggregates, so only their roots
/// have repositories. Entities inside an aggregate are reached through the root.
/// Repositories do not commit; commit is the <see cref="IUnitOfWork"/>'s job.
/// </summary>
public interface IRepository<TAggregate>
    where TAggregate : class, IAggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    void Update(TAggregate aggregate);

    void Remove(TAggregate aggregate);
}
