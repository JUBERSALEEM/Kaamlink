using KaamLink.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenges");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");

        builder.Property(o => o.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
        builder.Property(o => o.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
        builder.Property(o => o.AttemptCount).HasColumnName("attempt_count");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.ExpiresAt).HasColumnName("expires_at");
        builder.Property(o => o.ConsumedAt).HasColumnName("consumed_at");

        builder.HasIndex(o => new { o.PhoneNumber, o.CreatedAt }).HasDatabaseName("ix_otp_challenges_phone_created");
    }
}
