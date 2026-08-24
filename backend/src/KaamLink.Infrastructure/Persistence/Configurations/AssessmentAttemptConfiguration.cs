using KaamLink.Domain.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class AssessmentAttemptConfiguration : IEntityTypeConfiguration<AssessmentAttempt>
{
    public void Configure(EntityTypeBuilder<AssessmentAttempt> builder)
    {
        builder.ToTable("assessment_attempts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.WorkerProfileId).HasColumnName("worker_profile_id");
        builder.Property(a => a.SkillId).HasColumnName("skill_id");
        builder.Property(a => a.TotalQuestions).HasColumnName("total_questions");
        builder.Property(a => a.CorrectAnswers).HasColumnName("correct_answers");
        builder.Property(a => a.ScorePercent).HasColumnName("score_percent");
        builder.Property(a => a.Passed).HasColumnName("passed");
        builder.Property(a => a.StartedAt).HasColumnName("started_at");
        builder.Property(a => a.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(a => a.WorkerProfileId).HasDatabaseName("ix_assessment_attempts_worker_profile_id");
        builder.HasIndex(a => a.SkillId).HasDatabaseName("ix_assessment_attempts_skill_id");

        builder.HasOne<Domain.Workers.WorkerProfile>()
            .WithMany()
            .HasForeignKey(a => a.WorkerProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Skills.Skill>()
            .WithMany()
            .HasForeignKey(a => a.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Answers)
            .WithOne(a => a.Attempt)
            .HasForeignKey(a => a.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Answers).HasField("_answers");
    }
}
