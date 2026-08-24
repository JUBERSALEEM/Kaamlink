using KaamLink.Domain.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class AssessmentQuestionConfiguration : IEntityTypeConfiguration<AssessmentQuestion>
{
    public void Configure(EntityTypeBuilder<AssessmentQuestion> builder)
    {
        builder.ToTable("assessment_questions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id).HasColumnName("id");
        builder.Property(q => q.AssessmentId).HasColumnName("assessment_id");
        builder.Property(q => q.TextEn).HasColumnName("text_en").HasMaxLength(300).IsRequired();
        builder.Property(q => q.TextAr).HasColumnName("text_ar").HasMaxLength(300).IsRequired();
        builder.Property(q => q.OptionAEn).HasColumnName("option_a_en").HasMaxLength(200).IsRequired();
        builder.Property(q => q.OptionAAr).HasColumnName("option_a_ar").HasMaxLength(200).IsRequired();
        builder.Property(q => q.OptionBEn).HasColumnName("option_b_en").HasMaxLength(200).IsRequired();
        builder.Property(q => q.OptionBAr).HasColumnName("option_b_ar").HasMaxLength(200).IsRequired();
        builder.Property(q => q.OptionCEn).HasColumnName("option_c_en").HasMaxLength(200).IsRequired();
        builder.Property(q => q.OptionCAr).HasColumnName("option_c_ar").HasMaxLength(200).IsRequired();
        builder.Property(q => q.OptionDEn).HasColumnName("option_d_en").HasMaxLength(200).IsRequired();
        builder.Property(q => q.OptionDAr).HasColumnName("option_d_ar").HasMaxLength(200).IsRequired();
        builder.Property(q => q.CorrectOptionIndex).HasColumnName("correct_option_index");
        builder.Property(q => q.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(q => q.AssessmentId).HasDatabaseName("ix_assessment_questions_assessment_id");

        builder.HasOne(q => q.Assessment)
            .WithMany()
            .HasForeignKey(q => q.AssessmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
