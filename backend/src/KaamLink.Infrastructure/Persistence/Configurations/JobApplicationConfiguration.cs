using KaamLink.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("job_applications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.VacancyId).HasColumnName("vacancy_id");
        builder.Property(a => a.WorkerProfileId).HasColumnName("worker_profile_id");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.CoverNote).HasColumnName("cover_note").HasMaxLength(500);
        builder.Property(a => a.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        // One application per worker per vacancy — enforced at the DB level, not just in code.
        builder.HasIndex(a => new { a.VacancyId, a.WorkerProfileId })
            .IsUnique()
            .HasDatabaseName("ux_job_applications_vacancy_worker");
        builder.HasIndex(a => a.WorkerProfileId).HasDatabaseName("ix_job_applications_worker_profile_id");

        builder.HasOne<Domain.Jobs.Vacancy>()
            .WithMany()
            .HasForeignKey(a => a.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Workers.WorkerProfile>()
            .WithMany()
            .HasForeignKey(a => a.WorkerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
