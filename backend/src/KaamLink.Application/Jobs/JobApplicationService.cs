using System.Text.Json;
using KaamLink.Application.Abstractions;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Companies;
using KaamLink.Domain.Jobs;

namespace KaamLink.Application.Jobs;

/// <summary>
/// Job applications — the direct worker↔employer link. Rule anchors (product
/// decisions, documented in the Phase 04 report): apply requires completed worker
/// onboarding; one application per worker per vacancy; employer applicant lists
/// (they contain phone numbers) require a hiring role; terminal states are final,
/// and V1 treats Withdrawn as terminal too (no re-apply after withdrawal).
/// </summary>
public sealed class JobApplicationService(
    IJobApplicationRepository applications,
    IVacancyRepository vacancies,
    IWorkerProfileRepository workerProfiles,
    ICompanyRepository companies,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    TimeProvider timeProvider) : IJobApplicationService
{
    public async Task<ApplicationItemResponse> ApplyAsync(
        Guid userId, Guid vacancyId, ApplyRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        var vacancy = await vacancies.FindByIdAsync(vacancyId, ct);
        if (vacancy is null || vacancy.Status != VacancyStatus.Active)
            throw AppException.NotFound("jobs.vacancy_not_found", "Vacancy not found.");

        var profile = await workerProfiles.FindByUserIdAsync(userId, ct)
            ?? throw AppException.BadRequest("worker.profile_required", "Create your worker profile before applying.");
        if (!profile.OnboardingComplete)
            throw AppException.BadRequest("worker.onboarding_incomplete", "Complete your onboarding before applying.");

        if (!string.IsNullOrWhiteSpace(request.CoverNote) && request.CoverNote.Trim().Length > 500)
            throw AppException.BadRequest("jobs.cover_note_invalid", "Cover note is too long (max 500 characters).");

        if (await applications.FindByVacancyAndWorkerAsync(vacancyId, profile.Id, ct) is not null)
            throw AppException.BadRequest("jobs.already_applied", "You have already applied to this vacancy.");

        var application = JobApplication.Create(vacancyId, profile.Id, request.CoverNote, now);
        await applications.AddAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.ApplicationSubmitted,
            "job_application",
            application.Id.ToString(),
            actorUserId: userId,
            ct: ct);

        var company = await companies.FindByIdAsync(vacancy.CompanyId, ct);
        return ToItem(application, vacancy.Id, vacancy.Title, company?.Name ?? string.Empty);
    }

    public async Task<IReadOnlyList<ApplicationItemResponse>> ListMineAsync(
        Guid userId, CancellationToken ct = default)
    {
        var profile = await workerProfiles.FindByUserIdAsync(userId, ct);
        if (profile is null)
            return Array.Empty<ApplicationItemResponse>(); // no profile ⇒ no applications (honest empty)

        var items = await applications.ListByWorkerAsync(profile.Id, ct);
        var result = new List<ApplicationItemResponse>();
        foreach (var application in items)
        {
            var vacancy = await vacancies.FindByIdAsync(application.VacancyId, ct);
            var company = vacancy is null ? null : await companies.FindByIdAsync(vacancy.CompanyId, ct);
            result.Add(ToItem(
                application,
                application.VacancyId,
                vacancy?.Title ?? string.Empty,
                company?.Name ?? string.Empty));
        }

        return result;
    }

    public async Task<ApplicationItemResponse> WithdrawAsync(
        Guid userId, Guid applicationId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        var profile = await workerProfiles.FindByUserIdAsync(userId, ct);
        var application = await applications.FindByIdAsync(applicationId, ct);
        if (profile is null || application is null || application.WorkerProfileId != profile.Id)
            throw AppException.NotFound("jobs.application_not_found", "Application not found.");
        if (application.IsTerminal)
            throw AppException.BadRequest(
                "jobs.application_terminal", "A terminal application can no longer change.");

        application.Withdraw(now);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.ApplicationWithdrawn,
            "job_application",
            application.Id.ToString(),
            actorUserId: userId,
            ct: ct);

        var vacancy = await vacancies.FindByIdAsync(application.VacancyId, ct);
        var company = vacancy is null ? null : await companies.FindByIdAsync(vacancy.CompanyId, ct);
        return ToItem(
            application,
            application.VacancyId,
            vacancy?.Title ?? string.Empty,
            company?.Name ?? string.Empty);
    }

    public async Task<IReadOnlyList<VacancyApplicationItemResponse>> ListForVacancyAsync(
        Guid userId, Guid companyId, Guid vacancyId, CancellationToken ct = default)
    {
        await RequireMembershipAsync(companyId, userId, requireHiringRole: true, ct);

        var vacancy = await vacancies.FindByIdAsync(vacancyId, ct);
        if (vacancy is null || vacancy.CompanyId != companyId)
            throw AppException.NotFound("jobs.vacancy_not_found", "Vacancy not found.");

        var items = await applications.ListByVacancyAsync(vacancyId, ct);
        var snapshots = (await applications.GetApplicantSnapshotsAsync(
                items.Select(i => i.WorkerProfileId).ToList(), ct))
            .ToDictionary(s => s.WorkerProfileId);

        return items.Select(application =>
        {
            snapshots.TryGetValue(application.WorkerProfileId, out var snapshot);
            return new VacancyApplicationItemResponse(
                application.Id,
                application.Status.ToString(),
                application.CoverNote,
                application.WorkerProfileId,
                snapshot?.DisplayName ?? string.Empty,
                snapshot?.PhoneNumber ?? string.Empty,
                snapshot?.Emirate,
                snapshot?.PrimarySkillId,
                snapshot?.ProfileCompletenessPct ?? 0,
                application.CreatedAt);
        }).ToList();
    }

    public async Task<ApplicationStatusChangeResponse> ChangeStatusAsync(
        Guid userId, Guid companyId, Guid vacancyId, Guid applicationId,
        ChangeApplicationStatusRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        await RequireMembershipAsync(companyId, userId, requireHiringRole: true, ct);

        var vacancy = await vacancies.FindByIdAsync(vacancyId, ct);
        if (vacancy is null || vacancy.CompanyId != companyId)
            throw AppException.NotFound("jobs.vacancy_not_found", "Vacancy not found.");

        var application = await applications.FindByIdAsync(applicationId, ct);
        if (application is null || application.VacancyId != vacancyId)
            throw AppException.NotFound("jobs.application_not_found", "Application not found.");

        var target = Enum.TryParse<ApplicationStatus>(request.Status, ignoreCase: true, out var parsed)
            ? parsed
            : throw AppException.BadRequest("jobs.application_status_invalid", "Unknown application status.");
        if (target is ApplicationStatus.Applied or ApplicationStatus.Withdrawn)
            throw AppException.BadRequest(
                "jobs.application_status_invalid", "Employers can only view, shortlist, reject or hire.");
        if (application.IsTerminal)
            throw AppException.BadRequest(
                "jobs.application_terminal", "A terminal application can no longer change.");

        var from = application.Status;
        application.ApplyReview(target, now);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.ApplicationStatusChanged,
            "job_application",
            application.Id.ToString(),
            actorUserId: userId,
            metadataJson: JsonSerializer.Serialize(new
            {
                from = from.ToString(),
                to = target.ToString()
            }),
            ct: ct);

        return new ApplicationStatusChangeResponse(application.Id, application.Status.ToString(), application.ReviewedAt);
    }

    // ---- internals ----

    private async Task RequireMembershipAsync(Guid companyId, Guid userId, bool requireHiringRole, CancellationToken ct)
    {
        var membership = await companies.FindMembershipAsync(companyId, userId, ct)
            ?? throw AppException.NotFound("company.not_found", "Company not found.");

        if (requireHiringRole
            && membership.Role is not (CompanyRole.Owner or CompanyRole.Admin or CompanyRole.HiringManager))
        {
            throw AppException.Forbidden("jobs.role_not_allowed", "This membership role cannot manage applications.");
        }
    }

    private static ApplicationItemResponse ToItem(
        JobApplication application, Guid vacancyId, string vacancyTitle, string companyName) => new(
        application.Id,
        vacancyId,
        vacancyTitle,
        companyName,
        application.Status.ToString(),
        application.CoverNote,
        application.ReviewedAt,
        application.CreatedAt);
}
