using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Primitives;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Base configuration that every entity mapping inherits. It centralises the cross-cutting
/// concerns that live on <see cref="Entity"/> — primary key, optimistic concurrency, audit
/// columns, and the soft-delete query filter — so no single aggregate configuration repeats them.
/// This is the Template Method pattern: the base fixes the shared skeleton and defers the
/// aggregate-specific mapping to <see cref="ConfigureEntity"/>.
/// </summary>
internal abstract class EntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : Entity
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);

        // Application-assigned GUIDs (aggregates call Guid.NewGuid()), so EF must not try to
        // generate them. ValueGeneratedNever also stops EF treating a set Id as a temporary key.
        builder.Property(entity => entity.Id)
            .ValueGeneratedNever();

        // SQL Server rowversion: an 8-byte value the database bumps on every UPDATE. EF adds it to
        // the WHERE clause, so a concurrent overwrite throws DbUpdateConcurrencyException instead
        // of silently winning ("last write wins"). IsRowVersion maps it to the timestamp column.
        builder.Property(entity => entity.RowVersion)
            .IsRowVersion();

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc);

        builder.Property(entity => entity.DeletedAtUtc);

        builder.Property(entity => entity.CreatedBy);

        builder.Property(entity => entity.UpdatedBy);

        builder.Property(entity => entity.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Soft delete: rows are never physically removed (Remove() flips IsDeleted). This global
        // filter appends "WHERE IsDeleted = 0" to every query for TEntity, so deleted rows vanish
        // from normal reads without each query having to remember the condition.
        builder.HasQueryFilter(entity => !entity.IsDeleted);

        // Backing-field access: our entities expose read-only collections and private setters, so
        // EF must read/write the fields directly rather than through the public API.
        builder.HasIndex(entity => entity.IsDeleted)
            .HasFilter(null);

        ConfigureEntity(builder);
    }

    /// <summary>
    /// Implemented by each concrete configuration to map the columns, value objects, relationships,
    /// and indexes that are unique to its aggregate.
    /// </summary>
    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}
