using KaamLink.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>
{
    public void Configure(EntityTypeBuilder<Vacancy> builder)
    {
        builder.ToTable("vacancies");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");

        builder.Property(v => v.CompanyId).HasColumnName("company_id");
        builder.Property(v => v.CreatedByUserId).HasColumnName("created_by_user_id");

        builder.Property(v => v.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
        builder.Property(v => v.Description).HasColumnName("description").HasMaxLength(4000);

        builder.Property(v => v.Emirate)
            .HasColumnName("emirate")
            .HasConversion(new EnumToStringConverter<Domain.Shared.Emirate>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(v => v.Area).HasColumnName("area").HasMaxLength(120);

        builder.Property(v => v.SalaryMinAed).HasColumnName("salary_min_aed");
        builder.Property(v => v.SalaryMaxAed).HasColumnName("salary_max_aed");
        builder.Property(v => v.Headcount).HasColumnName("headcount");

        builder.Property(v => v.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");

        // Feed hot path: filter by status, sort newest first.
        builder.HasIndex(v => new { v.Status, v.CreatedAt }).HasDatabaseName("ix_vacancies_status_created_at");
        builder.HasIndex(v => v.CompanyId).HasDatabaseName("ix_vacancies_company_id");
        builder.HasIndex(v => v.CreatedByUserId).HasDatabaseName("ix_vacancies_created_by_user_id");

        // Vacancy dies with the company; creator users are never deleted in V1.
        builder.HasOne<Domain.Companies.Company>()
            .WithMany()
            .HasForeignKey(v => v.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Identity.User>()
            .WithMany()
            .HasForeignKey(v => v.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Skills)
            .WithOne(s => s.Vacancy)
            .HasForeignKey(s => s.VacancyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
