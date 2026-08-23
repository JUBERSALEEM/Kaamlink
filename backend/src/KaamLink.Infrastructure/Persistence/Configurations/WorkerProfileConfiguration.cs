using KaamLink.Domain.Skills;
using KaamLink.Domain.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class WorkerProfileConfiguration : IEntityTypeConfiguration<WorkerProfile>
{
    public void Configure(EntityTypeBuilder<WorkerProfile> builder)
    {
        builder.ToTable("worker_profiles");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");

        builder.Property(w => w.UserId).HasColumnName("user_id");
        builder.HasIndex(w => w.UserId).IsUnique().HasDatabaseName("ux_worker_profiles_user_id");

        builder.Property(w => w.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
        builder.Property(w => w.PrimarySkillId).HasColumnName("primary_skill_id");
        builder.HasIndex(w => w.PrimarySkillId).HasDatabaseName("ix_worker_profiles_primary_skill_id");

        // Nullable enums stored as text; converters skip nulls by default (ConvertsNulls=false).
        builder.Property(w => w.Emirate)
            .HasColumnName("emirate")
            .HasConversion(new EnumToStringConverter<Domain.Shared.Emirate>())
            .HasMaxLength(20);
        builder.Property(w => w.Area).HasColumnName("area").HasMaxLength(120);

        builder.Property(w => w.Availability)
            .HasColumnName("availability")
            .HasConversion(new EnumToStringConverter<Availability>())
            .HasMaxLength(20);

        builder.Property(w => w.ExpectedSalaryMinAed).HasColumnName("expected_salary_min_aed");
        builder.Property(w => w.ExpectedSalaryMaxAed).HasColumnName("expected_salary_max_aed");
        builder.Property(w => w.About).HasColumnName("about").HasMaxLength(2000);

        builder.Property(w => w.ProfileCompletenessPct).HasColumnName("profile_completeness_pct");
        builder.Property(w => w.OnboardingComplete).HasColumnName("onboarding_complete");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Skill>()
            .WithMany()
            .HasForeignKey(w => w.PrimarySkillId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK user_id → users (cascade): the profile is deleted with the account.
        builder.HasOne<Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(w => w.Skills)
            .WithOne(s => s.Profile)
            .HasForeignKey(s => s.WorkerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
