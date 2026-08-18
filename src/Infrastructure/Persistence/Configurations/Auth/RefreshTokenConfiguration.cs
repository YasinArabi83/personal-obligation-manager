using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POM.Auth.Jwt;

namespace POM.Persistence.Configurations.Auth;

/// <summary>
/// EF mapping for <see cref="RefreshToken"/> to the <c>refresh_tokens</c> table (docs/DATABASE.md §3,
/// plan 0004). Snake_case column naming comes from the global convention; names are set explicitly
/// for auditability, matching the <c>UserConfiguration</c> pattern.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);

        // App-assigned Guid PK (consistent with users — the app owns identity).
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(r => r.FamilyId)
            .IsRequired()
            .HasColumnName("family_id");

        builder.Property(r => r.TokenHash)
            .IsRequired()
            .HasColumnName("token_hash");

        builder.Property(r => r.ExpiresAt)
            .IsRequired()
            .HasColumnName("expires_at");

        builder.Property(r => r.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Look up tokens by their hash on every refresh; uniqueness prevents collision ambiguity.
        builder.HasIndex(r => r.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_token_hash");

        // Family-wide revocation scans by family_id among active tokens.
        builder.HasIndex(r => r.FamilyId)
            .HasDatabaseName("ix_refresh_tokens_family_id");

        // FK → users(id). A user's refresh tokens go with the user.
        builder.HasOne<POM.Users.User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
