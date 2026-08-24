using KaamLink.Domain.Auditing;
using KaamLink.Domain.Companies;
using KaamLink.Domain.Identity;
using KaamLink.Domain.Skills;
using KaamLink.Domain.Workers;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<WorkerProfile> WorkerProfiles => Set<WorkerProfile>();

    public DbSet<WorkerSkill> WorkerSkills => Set<WorkerSkill>();

    public DbSet<SkillCategory> SkillCategories => Set<SkillCategory>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();

    public DbSet<Domain.Jobs.Vacancy> Vacancies => Set<Domain.Jobs.Vacancy>();

    public DbSet<Domain.Jobs.VacancySkill> VacancySkills => Set<Domain.Jobs.VacancySkill>();

    public DbSet<Domain.Jobs.JobApplication> JobApplications => Set<Domain.Jobs.JobApplication>();

    public DbSet<Domain.Assessments.SkillAssessment> SkillAssessments => Set<Domain.Assessments.SkillAssessment>();

    public DbSet<Domain.Assessments.AssessmentQuestion> AssessmentQuestions => Set<Domain.Assessments.AssessmentQuestion>();

    public DbSet<Domain.Assessments.AssessmentAttempt> AssessmentAttempts => Set<Domain.Assessments.AssessmentAttempt>();

    public DbSet<Domain.Assessments.AssessmentAnswer> AssessmentAnswers => Set<Domain.Assessments.AssessmentAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
