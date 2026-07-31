using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectHub.Domain.Entities;
using ProjectHub.Persistence.Constants;

namespace ProjectHub.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="UserToken"/> child entity (email-confirmation / password-reset secrets). Like
/// <see cref="RefreshToken"/> it is not an aggregate root — it is minted and consumed only through the
/// <see cref="User"/> aggregate — yet it still inherits audit/soft-delete/concurrency columns from the
/// base configuration.
/// </summary>
internal sealed class UserTokenConfiguration : EntityConfiguration<UserToken>
{
    protected override void ConfigureEntity(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("user_tokens", Schemas.Identity);

        builder.Property(token => token.UserId)
            .IsRequired();

        // SHA-256 hash of the raw link token — the secret at rest. 128 fits any encoding of a 32-byte
        // digest with headroom, matching the refresh-token column for consistency.
        builder.Property(token => token.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        // Persist the discriminator as a compact int. We deliberately store the numeric enum value
        // (not the name string) so renaming the enum member never breaks existing rows.
        builder.Property(token => token.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .IsRequired();

        builder.Property(token => token.ConsumedAtUtc);

        // Redemption looks a token up by its hash; make it unique so two grants can never collide on
        // the same secret, and indexed so the lookup is a seek.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        // "Consume this user's live token of type X before issuing a new one" filters by UserId — a
        // composite index on (UserId, Type) makes that per-purpose lookup an index seek.
        builder.HasIndex(token => new { token.UserId, token.Type });

        builder.HasOne<User>()
            .WithMany(user => user.UserTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
