namespace ProjectHub.Application.Abstractions.Persistence;

/// <summary>
/// Represents the atomic commit boundary for one business transaction.
/// Repositories track changes; <see cref="SaveChangesAsync"/> flushes them together so partial
/// writes are impossible. This decouples the Application layer from EF Core's <c>DbContext</c>.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
