using KaamLink.Application.Abstractions;
using KaamLink.Application.Jobs;
using KaamLink.Domain.Companies;
using KaamLink.Domain.Identity;
using KaamLink.Domain.Jobs;
using KaamLink.Domain.Shared;
using KaamLink.Domain.Workers;

namespace KaamLink.UnitTests;

/// <summary>Phase 04 in-memory fakes + a harness producing fully wired jobs services.</summary>
internal static class JobTestHarness
{
    public static readonly Guid AluminiumSkillId = Guid.NewGuid();
    public static readonly Guid WelderSkillId = Guid.NewGuid();

    public sealed class Context
    {
        public InMemoryVacancyRepository Vacancies { get; } = new();
        public InMemoryCompanyRepository Companies { get; } = new();
        public InMemorySkillCatalogRepository SkillCatalog { get; } = new();
        public InMemoryWorkerProfileRepository WorkerProfiles { get; } = new();
        public InMemoryUserRepository Users { get; } = new();
        public InMemoryJobApplicationRepository Applications { get; }
        public SpyUnitOfWork UnitOfWork { get; } = new();
        public SpyAuditService Audit { get; } = new();

        public FixedTimeProvider Time { get; } =
            new(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));

        public Context()
        {
            Applications = new InMemoryJobApplicationRepository(WorkerProfiles, Users);
            SkillCatalog.SeedTestCatalog(AluminiumSkillId, WelderSkillId, Guid.NewGuid());
        }

        public VacancyService CreateVacancyService() => new(
            Vacancies, Companies, SkillCatalog, Applications, WorkerProfiles, UnitOfWork, Audit, Time);

        public JobApplicationService CreateApplicationService() => new(
            Applications, Vacancies, WorkerProfiles, Companies, UnitOfWork, Audit, Time);

        /// <summary>Company (optionally already verified) + a membership for the given user.</summary>
        public (Company Company, CompanyMember Membership) SeedCompany(
            Guid userId, bool verified, CompanyRole role = CompanyRole.Owner)
        {
            var now = Time.GetUtcNow();
            var company = Company.Create(
                "Gulf Fabrication LLC", BusinessCategory.Workshop, Emirate.Sharjah,
                "Industrial Area 12", "CN-1234567", null, now);
            if (verified)
                company.ApplyReview(approve: true, reason: null, now);

            Companies.Companies.Add(company);
            var membership = CompanyMember.Create(company.Id, userId, role, now, company);
            Companies.Members.Add(membership);
            return (company, membership);
        }

        /// <summary>Registered user with a completed worker onboarding (as Phase 03 defines it).</summary>
        public async Task<(User User, WorkerProfile Profile)> SeedOnboardedWorkerAsync(
            string phone = "+971501234567")
        {
            var now = Time.GetUtcNow();
            var user = User.Register(phone, isPlatformAdmin: false, now);
            Users.Store.Add(user);

            var profile = WorkerProfile.Create(user.Id, now);
            profile.ApplyBasics("Ahmed Ali", AluminiumSkillId, primarySkillProvided: true,
                Emirate.Sharjah, null, "6 years on site.", now);
            profile.ApplyAvailability(Availability.Immediately, 2500, 3500, now);
            profile.RecalculateProgress(hasAnySkill: true, now);
            await WorkerProfiles.AddAsync(profile);
            return (user, profile);
        }

        /// <summary>Active vacancy with one required skill (employer side already seeded separately).</summary>
        public async Task<Vacancy> SeedActiveVacancyAsync(
            Company company, Guid creatorUserId,
            string title = "Aluminium Fabricator", Emirate emirate = Emirate.Sharjah)
        {
            var now = Time.GetUtcNow();
            var vacancy = Vacancy.Create(company.Id, creatorUserId, emirate, now);
            vacancy.ApplyContent(title, null, emirate, "Industrial Area 12", 3000, 4000, 2, now);
            vacancy.ChangeStatus(VacancyStatus.Active, now);
            await Vacancies.AddAsync(vacancy);
            await Vacancies.ReplaceSkillsAsync(
                vacancy.Id, [VacancySkill.Create(vacancy.Id, AluminiumSkillId)]);
            return vacancy;
        }
    }
}

internal sealed class InMemoryVacancyRepository : IVacancyRepository
{
    public List<Vacancy> Store { get; } = [];
    public List<VacancySkill> Skills { get; } = [];

    public Task AddAsync(Vacancy vacancy, CancellationToken ct = default)
    {
        Store.Add(vacancy);
        return Task.CompletedTask;
    }

    public Task<Vacancy?> FindByIdAsync(Guid vacancyId, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(v => v.Id == vacancyId));

    public Task<IReadOnlyList<Vacancy>> SearchActiveAsync(
        Emirate? emirate, Guid? skillId, string? keyword, int skip, int take, CancellationToken ct = default)
    {
        var query = Store.Where(v => v.Status == VacancyStatus.Active);
        if (emirate is not null)
            query = query.Where(v => v.Emirate == emirate);
        if (skillId is not null)
            query = query.Where(v => Skills.Any(s => s.VacancyId == v.Id && s.SkillId == skillId.Value));
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(v => v.Title.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<IReadOnlyList<Vacancy>>(
            query.OrderByDescending(v => v.CreatedAt).Skip(skip).Take(take).ToList());
    }

    public Task<IReadOnlyList<Vacancy>> ListByCompanyAsync(Guid companyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Vacancy>>(
            Store.Where(v => v.CompanyId == companyId).OrderByDescending(v => v.CreatedAt).ToList());

    public Task ReplaceSkillsAsync(Guid vacancyId, IReadOnlyList<VacancySkill> skills, CancellationToken ct = default)
    {
        Skills.RemoveAll(s => s.VacancyId == vacancyId);
        Skills.AddRange(skills);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VacancySkill>> ListSkillsAsync(Guid vacancyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VacancySkill>>(
            Skills.Where(s => s.VacancyId == vacancyId).ToList());
}

internal sealed class InMemoryJobApplicationRepository(
    InMemoryWorkerProfileRepository workerProfiles,
    InMemoryUserRepository users) : IJobApplicationRepository
{
    public List<JobApplication> Store { get; } = [];

    public Task AddAsync(JobApplication application, CancellationToken ct = default)
    {
        Store.Add(application);
        return Task.CompletedTask;
    }

    public Task<JobApplication?> FindByIdAsync(Guid applicationId, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(a => a.Id == applicationId));

    public Task<JobApplication?> FindByVacancyAndWorkerAsync(
        Guid vacancyId, Guid workerProfileId, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(
            a => a.VacancyId == vacancyId && a.WorkerProfileId == workerProfileId));

    public Task<IReadOnlyList<JobApplication>> ListByWorkerAsync(
        Guid workerProfileId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<JobApplication>>(
            Store.Where(a => a.WorkerProfileId == workerProfileId)
                .OrderByDescending(a => a.CreatedAt).ToList());

    public Task<IReadOnlyList<JobApplication>> ListByVacancyAsync(
        Guid vacancyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<JobApplication>>(
            Store.Where(a => a.VacancyId == vacancyId).OrderByDescending(a => a.CreatedAt).ToList());

    public Task<IReadOnlyList<ApplicantSnapshot>> GetApplicantSnapshotsAsync(
        IReadOnlyCollection<Guid> workerProfileIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ApplicantSnapshot>>(
            workerProfiles.Store
                .Where(w => workerProfileIds.Contains(w.Id))
                .Select(w =>
                {
                    var user = users.Store.Single(u => u.Id == w.UserId);
                    return new ApplicantSnapshot(
                        w.Id, user.Id, w.DisplayName, user.PhoneNumber,
                        w.Emirate?.ToString(), w.PrimarySkillId, w.ProfileCompletenessPct);
                })
                .ToList());

    public Task<IReadOnlyDictionary<Guid, int>> CountByVacancyAsync(
        IReadOnlyCollection<Guid> vacancyIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(
            Store.Where(a => vacancyIds.Contains(a.VacancyId))
                .GroupBy(a => a.VacancyId)
                .ToDictionary(group => group.Key, group => group.Count()));
}
