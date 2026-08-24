using KaamLink.Application.Common;
using KaamLink.Application.Jobs;
using KaamLink.Domain.Companies;

namespace KaamLink.UnitTests.Jobs;

public sealed class VacancyServiceTests
{
    private static readonly UpsertVacancyRequest ValidRequest = new()
    {
        Title = "Aluminium Fabricator",
        Emirate = "Sharjah",
        Area = "Industrial Area 12",
        SalaryMinAed = 3000,
        SalaryMaxAed = 4000,
        Headcount = 2,
        SkillIds = [JobTestHarness.AluminiumSkillId]
    };

    [Fact]
    public async Task Create_RequiresMembership()
    {
        var harness = new JobTestHarness.Context();
        var service = harness.CreateVacancyService();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidRequest));
        Assert.Equal("company.not_found", exception.Code);
    }

    [Fact]
    public async Task Create_RejectsNonHiringRole()
    {
        var harness = new JobTestHarness.Context();
        var service = harness.CreateVacancyService();
        var userId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(userId, verified: true, role: CompanyRole.Viewer);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.CreateAsync(userId, company.Id, ValidRequest));
        Assert.Equal("jobs.role_not_allowed", exception.Code);
    }

    [Fact]
    public async Task Create_PersistsDraft_WithSkills_AndAudits()
    {
        var harness = new JobTestHarness.Context();
        var service = harness.CreateVacancyService();
        var userId = Guid.NewGuid();
        harness.SeedCompany(userId, verified: false); // Drafts are allowed for pending companies

        var created = await service.CreateAsync(userId, harness.Companies.Companies[0].Id, ValidRequest);

        Assert.Equal("Draft", created.Status);
        Assert.Equal("Sharjah", created.Emirate);
        Assert.Equal(0, created.ApplicationCount);
        Assert.Single(harness.Vacancies.Store);
        Assert.Single(harness.Vacancies.Skills);
        Assert.Contains(harness.Audit.Records, r => r.Action == KaamLink.Domain.Auditing.AuditActions.VacancyCreated);
    }

    [Fact]
    public async Task Activate_RequiresVerifiedCompany()
    {
        var harness = new JobTestHarness.Context();
        var service = harness.CreateVacancyService();
        var userId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(userId, verified: false);

        var created = await service.CreateAsync(userId, company.Id, ValidRequest);
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ChangeStatusAsync(userId, company.Id, created.Id, new ChangeVacancyStatusRequest { Status = "Active" }));
        Assert.Equal("jobs.company_not_verified", exception.Code);
    }

    [Fact]
    public async Task Activate_Works_WhenVerified_AndAuditsTransition()
    {
        var harness = new JobTestHarness.Context();
        var service = harness.CreateVacancyService();
        var userId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(userId, verified: true);

        var created = await service.CreateAsync(userId, company.Id, ValidRequest);
        var activated = await service.ChangeStatusAsync(
            userId, company.Id, created.Id, new ChangeVacancyStatusRequest { Status = "Active" });

        Assert.Equal("Active", activated.Status);
        var audit = Assert.Single(harness.Audit.Records,
            r => r.Action == KaamLink.Domain.Auditing.AuditActions.VacancyStatusChanged);
        Assert.Contains("Draft", audit.MetadataJson);
        Assert.Contains("Active", audit.MetadataJson);
    }

    [Fact]
    public async Task Feed_ReturnsActiveOnly_NewestFirst()
    {
        var harness = new JobTestHarness.Context();
        var employerId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(employerId, verified: true);
        var active = await harness.SeedActiveVacancyAsync(company, employerId, "Aluminium Fabricator");
        var draft = KaamLink.Domain.Jobs.Vacancy.Create(
            company.Id, employerId, KaamLink.Domain.Shared.Emirate.Dubai, harness.Time.GetUtcNow());
        draft.ApplyContent("Draft Posting", null, KaamLink.Domain.Shared.Emirate.Dubai, null, null, null, 1,
            harness.Time.GetUtcNow());
        await harness.Vacancies.AddAsync(draft);

        var service = harness.CreateVacancyService();
        var page = await service.SearchActiveAsync(null, null, null, page: 1, pageSize: 10);

        var item = Assert.Single(page.Items);
        Assert.Equal(active.Id, item.Id);
        Assert.Equal("Gulf Fabrication LLC", item.CompanyName);
        Assert.Equal("Verified", item.CompanyVerificationState);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task Feed_Filters_Emirate_And_Keyword()
    {
        var harness = new JobTestHarness.Context();
        var employerId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(employerId, verified: true);
        await harness.SeedActiveVacancyAsync(company, employerId, "Aluminium Fabricator", KaamLink.Domain.Shared.Emirate.Sharjah);
        await harness.SeedActiveVacancyAsync(company, employerId, "Welder Night Shift", KaamLink.Domain.Shared.Emirate.Dubai);

        var service = harness.CreateVacancyService();

        var dubaiOnly = await service.SearchActiveAsync("Dubai", null, null, 1, 10);
        Assert.Single(dubaiOnly.Items);

        var badEmirate = await Assert.ThrowsAsync<AppException>(
            () => service.SearchActiveAsync("Nowhere", null, null, 1, 10));
        Assert.Equal("jobs.emirate_invalid", badEmirate.Code);

        var keyword = await service.SearchActiveAsync(null, null, "welder", 1, 10);
        Assert.Single(keyword.Items);
    }

    [Fact]
    public async Task Detail_Hides_NonActive_FromWorkers()
    {
        var harness = new JobTestHarness.Context();
        var service = harness.CreateVacancyService();
        var employerId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(employerId, verified: true);
        var created = await service.CreateAsync(employerId, company.Id, ValidRequest); // stays Draft

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.GetActiveDetailAsync(Guid.NewGuid(), created.Id));
        Assert.Equal("jobs.vacancy_not_found", exception.Code);
    }

    [Fact]
    public async Task Update_Rejects_Closed()
    {
        var harness = new JobTestHarness.Context();
        var service = harness.CreateVacancyService();
        var userId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(userId, verified: true);

        var created = await service.CreateAsync(userId, company.Id, ValidRequest);
        await service.ChangeStatusAsync(userId, company.Id, created.Id, new ChangeVacancyStatusRequest { Status = "Closed" });

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.UpdateAsync(userId, company.Id, created.Id, ValidRequest));
        Assert.Equal("jobs.vacancy_closed", exception.Code);
    }

    [Fact]
    public async Task Validation_Catches_Salary_Mistakes()
    {
        var harness = new JobTestHarness.Context();
        var service = harness.CreateVacancyService();
        var userId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(userId, verified: true);

        var minOnly = new UpsertVacancyRequest
        {
            Title = "Welder",
            Emirate = "Dubai",
            SalaryMinAed = 3000,
            SkillIds = [JobTestHarness.WelderSkillId]
        };
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.CreateAsync(userId, company.Id, minOnly));
        Assert.Equal("jobs.salary_pair_required", exception.Code);
    }
}
