using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="RefreshToken"/> child entity. Like <see cref="UserRole"/> it is not an
/// aggregate root — it is created only through the <see cref="User"/> aggregate — but it still
/// inherits the audit/soft-delete/concurrency columns from the base configuration.
/// </summary>
internal sealed class RefreshTokenConfiguration : EntityConfiguration<RefreshToken>
{
    protected override void ConfigureEntity(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", Schemas.Identity);

        builder.Property(token => token.UserId)
            .IsRequired();

        // The SHA-256 hash rendered as a fixed-length hex/base64 string. 128 comfortably fits any
        // encoding of a 32-byte digest and leaves room if we lengthen the hash later.
        builder.Property(token => token.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .IsRequired();

        builder.Property(token => token.RevokedAtUtc);

        builder.Property(token => token.ReplacedByTokenHash)
            .HasMaxLength(128);

        // Refresh flow looks a token up by its hash on every /refresh call — index it, and make it
        // unique so two grants can never collide on the same secret.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        // "Give me this user's tokens" (rotation, revoke-all) filters by UserId — index that access
        // path too so those queries stay index seeks, not table scans.
        builder.HasIndex(token => token.UserId);

        // FK to the owning User. No reverse WithMany() here because the collection navigation is
        // already configured from the User side (below), keeping a single source of truth.
        builder.HasOne<User>()
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
