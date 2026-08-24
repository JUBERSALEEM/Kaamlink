using KaamLink.Domain.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class WorkerSkillConfiguration : IEntityTypeConfiguration<WorkerSkill>
{
    public void Configure(EntityTypeBuilder<WorkerSkill> builder)
    {
        builder.ToTable("worker_skills");

        builder.HasKey(w => new { w.WorkerProfileId, w.SkillId });

        builder.Property(w => w.WorkerProfileId).HasColumnName("worker_profile_id");
        builder.Property(w => w.SkillId).HasColumnName("skill_id");
        builder.Property(w => w.YearsExperience).HasColumnName("years_experience");
        builder.Property(w => w.IsPrimary).HasColumnName("is_primary");

        builder.Property(w => w.VerificationState)
            .HasColumnName("verification_state")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(w => w.AssessmentVerifiedAt).HasColumnName("assessment_verified_at");

        builder.HasIndex(w => w.SkillId).HasDatabaseName("ix_worker_skills_skill_id");

        builder.HasOne(w => w.Skill)
            .WithMany()
            .HasForeignKey(w => w.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
