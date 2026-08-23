using KaamLink.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at");
        builder.Property(r => r.RevokedAt).HasColumnName("revoked_at");
        builder.Property(r => r.ReplacedByTokenId).HasColumnName("replaced_by_token_id");

        builder.HasIndex(r => r.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_refresh_tokens_user_id");

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
