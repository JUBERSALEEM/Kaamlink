using KaamLink.Application.Common;
using KaamLink.Application.Jobs;
using KaamLink.Domain.Companies;
using KaamLink.Domain.Jobs;

namespace KaamLink.UnitTests.Jobs;

public sealed class JobApplicationServiceTests
{
    private static async
        Task<(JobTestHarness.Context Harness, KaamLink.Domain.Identity.User Worker, Vacancy Vacancy)>
        SetupWithActiveVacancyAsync()
    {
        var harness = new JobTestHarness.Context();
        var employerId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(employerId, verified: true);
        var vacancy = await harness.SeedActiveVacancyAsync(company, employerId);
        var (worker, _) = await harness.SeedOnboardedWorkerAsync();
        return (harness, worker, vacancy);
    }

    [Fact]
    public async Task Apply_RequiresProfile()
    {
        var harness = new JobTestHarness.Context();
        var employerId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(employerId, verified: true);
        var vacancy = await harness.SeedActiveVacancyAsync(company, employerId);
        var service = harness.CreateApplicationService();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ApplyAsync(Guid.NewGuid(), vacancy.Id, new ApplyRequest()));
        Assert.Equal("worker.profile_required", exception.Code);
    }

    [Fact]
    public async Task Apply_RequiresCompletedOnboarding()
    {
        var harness = new JobTestHarness.Context();
        var employerId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(employerId, verified: true);
        var vacancy = await harness.SeedActiveVacancyAsync(company, employerId);

        var now = harness.Time.GetUtcNow();
        var user = KaamLink.Domain.Identity.User.Register("+971509999999", isPlatformAdmin: false, now);
        harness.Users.Store.Add(user);
        await harness.WorkerProfiles.AddAsync(
            KaamLink.Domain.Workers.WorkerProfile.Create(user.Id, now)); // untouched ⇒ incomplete

        var service = harness.CreateApplicationService();
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ApplyAsync(user.Id, vacancy.Id, new ApplyRequest()));
        Assert.Equal("worker.onboarding_incomplete", exception.Code);
    }

    [Fact]
    public async Task Apply_ToNonActiveVacancy_NotFound()
    {
        var harness = new JobTestHarness.Context();
        var employerId = Guid.NewGuid();
        var (company, _) = harness.SeedCompany(employerId, verified: true);
        var (worker, _) = await harness.SeedOnboardedWorkerAsync();

        var now = harness.Time.GetUtcNow();
        var draft = Vacancy.Create(company.Id, employerId, KaamLink.Domain.Shared.Emirate.Dubai, now);
        draft.ApplyContent("Draft", null, KaamLink.Domain.Shared.Emirate.Dubai, null, null, null, 1, now);
        await harness.Vacancies.AddAsync(draft);

        var service = harness.CreateApplicationService();
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ApplyAsync(worker.Id, draft.Id, new ApplyRequest()));
        Assert.Equal("jobs.vacancy_not_found", exception.Code);
    }

    [Fact]
    public async Task Apply_Succeeds_DressesCorrectly_AndAudits()
    {
        var (harness, worker, vacancy) = await SetupWithActiveVacancyAsync();
        var service = harness.CreateApplicationService();

        var applied = await service.ApplyAsync(worker.Id, vacancy.Id, new ApplyRequest { CoverNote = "Available tomorrow." });

        Assert.Equal("Applied", applied.Status);
        Assert.Equal(vacancy.Title, applied.VacancyTitle);
        Assert.Equal("Gulf Fabrication LLC", applied.CompanyName);
        Assert.Equal("Available tomorrow.", applied.CoverNote);
        Assert.Single(harness.Applications.Store);
        Assert.Contains(harness.Audit.Records,
            r => r.Action == KaamLink.Domain.Auditing.AuditActions.ApplicationSubmitted);
    }

    [Fact]
    public async Task Apply_Duplicate_IsRejected()
    {
        var (harness, worker, vacancy) = await SetupWithActiveVacancyAsync();
        var service = harness.CreateApplicationService();

        await service.ApplyAsync(worker.Id, vacancy.Id, new ApplyRequest());
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ApplyAsync(worker.Id, vacancy.Id, new ApplyRequest()));
        Assert.Equal("jobs.already_applied", exception.Code);
    }

    [Fact]
    public async Task EmployerFlow_ShortlistHire_ThenTerminal()
    {
        var (harness, worker, vacancy) = await SetupWithActiveVacancyAsync();
        var service = harness.CreateApplicationService();
        var employerId = harness.Companies.Members[0].UserId;
        var companyId = vacancy.CompanyId;

        var applied = await service.ApplyAsync(worker.Id, vacancy.Id, new ApplyRequest());

        var applicants = await service.ListForVacancyAsync(employerId, companyId, vacancy.Id);
        var applicant = Assert.Single(applicants);
        Assert.Equal("Ahmed Ali", applicant.WorkerDisplayName);
        Assert.Equal("+971501234567", applicant.WorkerPhoneNumber);
        Assert.Equal(100, applicant.WorkerProfileCompletenessPct);

        await service.ChangeStatusAsync(employerId, companyId, vacancy.Id, applied.Id,
            new ChangeApplicationStatusRequest { Status = "Shortlisted" });
        var hired = await service.ChangeStatusAsync(employerId, companyId, vacancy.Id, applied.Id,
            new ChangeApplicationStatusRequest { Status = "Hired" });
        Assert.Equal("Hired", hired.Status);
        Assert.NotNull(hired.ReviewedAt);

        var terminal = await Assert.ThrowsAsync<AppException>(
            () => service.ChangeStatusAsync(employerId, companyId, vacancy.Id, applied.Id,
                new ChangeApplicationStatusRequest { Status = "Rejected" }));
        Assert.Equal("jobs.application_terminal", terminal.Code);
    }

    [Fact]
    public async Task EmployerLists_RequireHiringRole()
    {
        var (harness, worker, vacancy) = await SetupWithActiveVacancyAsync();
        var service = harness.CreateApplicationService();
        await service.ApplyAsync(worker.Id, vacancy.Id, new ApplyRequest());

        var viewerId = Guid.NewGuid();
        var now = harness.Time.GetUtcNow();
        harness.Companies.Members.Add(CompanyMember.Create(
            vacancy.CompanyId, viewerId, CompanyRole.Viewer, now));

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ListForVacancyAsync(viewerId, vacancy.CompanyId, vacancy.Id));
        Assert.Equal("jobs.role_not_allowed", exception.Code);
    }

    [Fact]
    public async Task Withdraw_BlocksTerminal_AndForeignApplications()
    {
        var (harness, worker, vacancy) = await SetupWithActiveVacancyAsync();
        var service = harness.CreateApplicationService();
        var employerId = harness.Companies.Members[0].UserId;

        var applied = await service.ApplyAsync(worker.Id, vacancy.Id, new ApplyRequest());

        var withdrawn = await service.WithdrawAsync(worker.Id, applied.Id);
        Assert.Equal("Withdrawn", withdrawn.Status);
        Assert.Contains(harness.Audit.Records,
            r => r.Action == KaamLink.Domain.Auditing.AuditActions.ApplicationWithdrawn);

        var again = await Assert.ThrowsAsync<AppException>(() => service.WithdrawAsync(worker.Id, applied.Id));
        Assert.Equal("jobs.application_terminal", again.Code);

        var foreign = await Assert.ThrowsAsync<AppException>(() => service.WithdrawAsync(Guid.NewGuid(), applied.Id));
        Assert.Equal("jobs.application_not_found", foreign.Code);

        var employerDenied = await Assert.ThrowsAsync<AppException>(
            () => service.ChangeStatusAsync(employerId, vacancy.CompanyId, vacancy.Id, applied.Id,
                new ChangeApplicationStatusRequest { Status = "Shortlisted" }));
        Assert.Equal("jobs.application_terminal", employerDenied.Code);
    }

    [Fact]
    public async Task ListMine_ReturnsOwnApplicationsOnly()
    {
        var (harness, worker, vacancy) = await SetupWithActiveVacancyAsync();
        var service = harness.CreateApplicationService();

        var (otherWorker, _) = await harness.SeedOnboardedWorkerAsync("+971502222222");
        await service.ApplyAsync(worker.Id, vacancy.Id, new ApplyRequest());
        await service.ApplyAsync(otherWorker.Id, vacancy.Id, new ApplyRequest());

        var mine = await service.ListMineAsync(worker.Id);
        var entry = Assert.Single(mine);
        Assert.Equal(vacancy.Id, entry.VacancyId);
        Assert.Equal("Applied", entry.Status);

        var none = await service.ListMineAsync(Guid.NewGuid());
        Assert.Empty(none); // no profile ⇒ honest empty list
    }
}
