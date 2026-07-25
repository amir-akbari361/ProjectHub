using Microsoft.EntityFrameworkCore;
using ProjectHub.Application.Abstractions.Persistence;
using ProjectHub.Domain.Abstractions;

namespace ProjectHub.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the generic write-side <see cref="IRepository{TAggregate}"/>.
/// It is deliberately thin: it only tracks intent (Add/Update/Remove) and loads by key. It never
/// calls SaveChanges — that is the <see cref="IUnitOfWork"/>'s single responsibility, so several
/// repository operations across different aggregates commit atomically in one transaction.
/// </summary>
internal sealed class Repository<TAggregate> : IRepository<TAggregate>
    where TAggregate : class, IAggregateRoot
{
    private readonly ApplicationDbContext _context;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
    }

    // FindAsync hits the identity map first: if the aggregate is already tracked, no round-trip to
    // the database occurs. It also respects the soft-delete global query filter.
    public async Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Set<TAggregate>().FindAsync([id], cancellationToken);

    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default) =>
        await _context.Set<TAggregate>().AddAsync(aggregate, cancellationToken);

    // Update/Remove are synchronous: they only mutate the ChangeTracker state, no I/O happens here.
    public void Update(TAggregate aggregate) =>
        _context.Set<TAggregate>().Update(aggregate);

    // Marks the entry Deleted; the SoftDeleteInterceptor rewrites this into an UPDATE at save time.
    public void Remove(TAggregate aggregate) =>
        _context.Set<TAggregate>().Remove(aggregate);
}
