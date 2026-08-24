using KaamLink.Domain.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class AssessmentAnswerConfiguration : IEntityTypeConfiguration<AssessmentAnswer>
{
    public void Configure(EntityTypeBuilder<AssessmentAnswer> builder)
    {
        builder.ToTable("assessment_answers");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.AttemptId).HasColumnName("attempt_id");
        builder.Property(a => a.QuestionId).HasColumnName("question_id");
        builder.Property(a => a.SelectedOptionIndex).HasColumnName("selected_option_index");

        // One answer per question per attempt; unique index covers the FK prefix (no extra ix).
        builder.HasIndex(a => new { a.AttemptId, a.QuestionId })
            .IsUnique()
            .HasDatabaseName("ux_assessment_answers_attempt_question");

        builder.HasIndex(a => a.QuestionId).HasDatabaseName("ix_assessment_answers_question_id");

        builder.HasOne<AssessmentQuestion>()
            .WithMany()
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
