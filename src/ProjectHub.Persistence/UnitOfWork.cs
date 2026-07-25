using ProjectHub.Application.Abstractions.Persistence;

namespace ProjectHub.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUnitOfWork"/>. It owns the single commit boundary for a
/// business transaction: it delegates to the shared <see cref="ApplicationDbContext"/> whose
/// <c>SaveChangesAsync</c> flushes every tracked change in one round-trip and one implicit
/// transaction. Because the context is registered Scoped, the repositories and this unit of work
/// share the same instance within a request, so their changes commit together atomically.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
