using KaamLink.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.CategoryId).HasColumnName("category_id");
        builder.Property(s => s.NameEn).HasColumnName("name_en").HasMaxLength(120).IsRequired();
        builder.Property(s => s.NameAr).HasColumnName("name_ar").HasMaxLength(160).IsRequired();
        builder.Property(s => s.RequiresCertification).HasColumnName("requires_certification");
        builder.Property(s => s.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(s => s.CategoryId).HasDatabaseName("ix_skills_category_id");
    }
}
