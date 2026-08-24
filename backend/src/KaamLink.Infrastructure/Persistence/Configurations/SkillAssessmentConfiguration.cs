using KaamLink.Domain.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class SkillAssessmentConfiguration : IEntityTypeConfiguration<SkillAssessment>
{
    public void Configure(EntityTypeBuilder<SkillAssessment> builder)
    {
        builder.ToTable("skill_assessments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.SkillId).HasColumnName("skill_id");
        builder.Property(a => a.PassMarkPercent).HasColumnName("pass_mark_percent");
        builder.Property(a => a.IsActive).HasColumnName("is_active");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");

        // One active template per skill (V1).
        builder.HasIndex(a => a.SkillId)
            .IsUnique()
            .HasDatabaseName("ux_skill_assessments_skill_id");

        builder.HasOne(a => a.Skill)
            .WithMany()
            .HasForeignKey(a => a.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
