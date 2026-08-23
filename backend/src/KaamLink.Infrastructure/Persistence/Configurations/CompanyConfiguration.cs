using KaamLink.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        builder.Property(c => c.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.Emirate).HasColumnName("emirate").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Area).HasColumnName("area").HasMaxLength(120);
        builder.Property(c => c.TradeLicenseNumber).HasColumnName("trade_license_number").HasMaxLength(40);
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(2000);

        builder.Property(c => c.VerificationState)
            .HasColumnName("verification_state")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(c => c.VerificationReason).HasColumnName("verification_reason").HasMaxLength(500);
        builder.Property(c => c.VerifiedAt).HasColumnName("verified_at");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        // Unique licence number only where one is actually provided (PostgreSQL filtered index).
        builder.HasIndex(c => c.TradeLicenseNumber)
            .IsUnique()
            .HasFilter("trade_license_number IS NOT NULL")
            .HasDatabaseName("ux_companies_trade_license_number");

        builder.HasIndex(c => c.VerificationState).HasDatabaseName("ix_companies_verification_state");
    }
}
