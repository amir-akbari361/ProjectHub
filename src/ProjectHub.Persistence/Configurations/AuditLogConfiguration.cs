using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="AuditLog"/> aggregate root — an append-only trail. The (EntityName, EntityId)
/// index backs "show the history of this record". Changes stores a JSON diff as free-form text.
/// </summary>
internal sealed class AuditLogConfiguration : EntityConfiguration<AuditLog>
{
    protected override void ConfigureEntity(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", Schemas.Auditing);

        builder.Property(log => log.EntityName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(log => log.EntityId)
            .IsRequired();

        builder.Property(log => log.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(log => log.PerformedBy);

        // Changes is an unbounded JSON payload; map it to nvarchar(max) via no max length.
        builder.Property(log => log.Changes);

        // Denormalised owning-project id. Nullable, so no default and no data backfill is needed for
        // existing rows — consistent with the additive-only migration policy.
        builder.Property(log => log.ProjectId);

        builder.HasIndex(log => new { log.EntityName, log.EntityId });

        // Backs both the membership-scoped read (Part B3) and the admin viewer's project filter (Part C).
        builder.HasIndex(log => log.ProjectId);
    }
}
