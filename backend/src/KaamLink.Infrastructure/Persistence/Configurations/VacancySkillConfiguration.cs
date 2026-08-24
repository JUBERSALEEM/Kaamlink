using KaamLink.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class VacancySkillConfiguration : IEntityTypeConfiguration<VacancySkill>
{
    public void Configure(EntityTypeBuilder<VacancySkill> builder)
    {
        builder.ToTable("vacancy_skills");

        builder.HasKey(v => new { v.VacancyId, v.SkillId });

        builder.Property(v => v.VacancyId).HasColumnName("vacancy_id");
        builder.Property(v => v.SkillId).HasColumnName("skill_id");

        builder.HasIndex(v => v.SkillId).HasDatabaseName("ix_vacancy_skills_skill_id");

        builder.HasOne(v => v.Skill)
            .WithMany()
            .HasForeignKey(v => v.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
