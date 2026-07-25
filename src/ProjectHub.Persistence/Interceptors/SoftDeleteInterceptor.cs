using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProjectHub.Application.Abstractions.Services;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Persistence.Interceptors;

/// <summary>
/// Turns a physical <c>DELETE</c> into a logical (soft) delete. When the repository calls
/// <c>Remove</c>, EF marks the entry <see cref="EntityState.Deleted"/>. This interceptor catches
/// that just before the SQL is generated, flips the state to <see cref="EntityState.Modified"/>,
/// and stamps <c>IsDeleted</c>/<c>DeletedAtUtc</c>. Combined with the global query filter in
/// <see cref="Configurations.EntityConfiguration{TEntity}"/> the row disappears from reads but is
/// never actually removed — history and referential integrity are preserved.
/// </summary>
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public SoftDeleteInterceptor(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ConvertDeletesToSoftDeletes(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ConvertDeletesToSoftDeletes(DbContext context)
    {
        var deletedEntries = context.ChangeTracker
            .Entries<Entity>()
            .Where(entry => entry.State == EntityState.Deleted);

        foreach (var entry in deletedEntries)
        {
            // Downgrade the operation from DELETE to UPDATE. EF then emits an UPDATE that only
            // touches the columns whose CurrentValue we change below.
            entry.State = EntityState.Modified;

            // EF exposes CurrentValue through its own metadata, so it can write these even though
            // the C# setters are private — no need to leak a public "Delete" method onto Entity.
            entry.Property(nameof(Entity.IsDeleted)).CurrentValue = true;
            entry.Property(nameof(Entity.DeletedAtUtc)).CurrentValue = _dateTimeProvider.UtcNow;
            entry.Property(nameof(Entity.UpdatedAtUtc)).CurrentValue = _dateTimeProvider.UtcNow;
        }
    }
}
