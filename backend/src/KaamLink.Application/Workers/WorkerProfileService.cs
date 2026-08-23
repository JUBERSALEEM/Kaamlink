using KaamLink.Application.Abstractions;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Shared;
using KaamLink.Domain.Workers;

namespace KaamLink.Application.Workers;

/// <summary>
/// Worker profile + Skill Passport (self-declared layer). Onboarding is progressive:
/// every call is a patch; the server recomputes completeness and the canonical
/// "onboarding complete" flag — clients never define their own.
/// </summary>
public sealed class WorkerProfileService(
    IWorkerProfileRepository workerProfiles,
    ISkillCatalogRepository skillCatalog,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    TimeProvider timeProvider) : IWorkerProfileService
{
    public async Task<WorkerProfileResponse> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await workerProfiles.FindByUserIdAsync(userId, ct)
            ?? throw AppException.NotFound("worker.profile_not_found", "No worker profile exists yet.");
        return await ToResponseAsync(profile, ct);
    }

    public async Task<WorkerProfileResponse> UpdateProfileAsync(
        Guid userId, UpdateWorkerProfileRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var (profile, isNew) = await GetOrCreateAsync(userId, now, ct);

        if (request.DisplayName is not null && (request.DisplayName.Trim().Length is < 2 or > 120))
            throw AppException.BadRequest("worker.display_name_invalid", "Name must be 2–120 characters.");

        if (request.PrimarySkillProvided
            && request.PrimarySkillId is not null
            && !await skillCatalog.SkillExistsAsync(request.PrimarySkillId.Value, ct))
            throw AppException.BadRequest("worker.skill_unknown", "Unknown profession/skill.");

        var emirate = ParseEmirate(request.Emirate);
        var availability = ParseAvailability(request.Availability);

        ValidateSalary(
            request.ExpectedSalaryMinAed ?? profile.ExpectedSalaryMinAed,
            request.ExpectedSalaryMaxAed ?? profile.ExpectedSalaryMaxAed);

        profile.ApplyBasics(
            request.DisplayName,
            request.PrimarySkillId,
            request.PrimarySkillProvided,
            emirate,
            request.Area,
            request.About,
            now);
        profile.ApplyAvailability(
            availability,
            request.ExpectedSalaryMinAed,
            request.ExpectedSalaryMaxAed,
            now);

        var skills = await workerProfiles.ListSkillsAsync(profile.Id, ct);
        profile.RecalculateProgress(skills.Count > 0, now);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.WorkerProfileUpdated,
            "worker_profile",
            profile.Id.ToString(),
            actorUserId: userId,
            ct: ct);

        if (isNew)
            await unitOfWork.SaveChangesAsync(ct); // (no-op safeguard; Add is already tracked)

        return await ToResponseAsync(profile, ct);
    }

    public async Task<WorkerProfileResponse> SetSkillsAsync(
        Guid userId, SetWorkerSkillsRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var (profile, _) = await GetOrCreateAsync(userId, now, ct);

        var distinct = request.Skills
            .GroupBy(s => s.SkillId)
            .Select(g => g.First())
            .ToList();

        foreach (var item in distinct)
        {
            if (!await skillCatalog.SkillExistsAsync(item.SkillId, ct))
                throw AppException.BadRequest("worker.skill_unknown", $"Unknown skill: {item.SkillId}.");
            if (item.YearsExperience is < 0 or > 50)
                throw AppException.BadRequest("worker.years_invalid", "Experience must be 0–50 years.");
        }

        var skills = distinct
            .Select(s => WorkerSkill.Create(
                profile.Id,
                s.SkillId,
                s.YearsExperience,
                isPrimary: s.SkillId == profile.PrimarySkillId))
            .ToList();

        await workerProfiles.ReplaceSkillsAsync(profile.Id, skills, ct);
        profile.RecalculateProgress(skills.Count > 0, now);
        await unitOfWork.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditActions.WorkerSkillsUpdated,
            "worker_profile",
            profile.Id.ToString(),
            actorUserId: userId,
            ct: ct);

        return await ToResponseAsync(profile, ct);
    }

    // ---- internals ----

    private async Task<(WorkerProfile Profile, bool IsNew)> GetOrCreateAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await workerProfiles.FindByUserIdAsync(userId, ct);
        if (existing is not null)
            return (existing, false);

        var created = WorkerProfile.Create(userId, now);
        await workerProfiles.AddAsync(created, ct);
        return (created, true);
    }

    private async Task<WorkerProfileResponse> ToResponseAsync(WorkerProfile profile, CancellationToken ct)
    {
        var skills = await workerProfiles.ListSkillsAsync(profile.Id, ct);
        var items = new List<WorkerSkillItem>();

        foreach (var workerSkill in skills)
        {
            var skill = await skillCatalog.FindSkillAsync(workerSkill.SkillId, ct);
            if (skill is null)
                continue; // reference data removed by an admin; keep response honest
            items.Add(new WorkerSkillItem(
                skill.Id,
                skill.NameEn,
                skill.NameAr,
                skill.RequiresCertification,
                workerSkill.YearsExperience,
                workerSkill.IsPrimary,
                workerSkill.VerificationState.ToString()));
        }

        return new WorkerProfileResponse(
            profile.Id,
            profile.DisplayName,
            profile.PrimarySkillId,
            profile.Emirate?.ToString(),
            profile.Area,
            profile.Availability?.ToString(),
            profile.ExpectedSalaryMinAed,
            profile.ExpectedSalaryMaxAed,
            profile.About,
            profile.ProfileCompletenessPct,
            profile.OnboardingComplete,
            items);
    }

    private static Emirate? ParseEmirate(string? value)
    {
        if (value is null)
            return null;
        return Enum.TryParse<Emirate>(value, ignoreCase: true, out var emirate)
            ? emirate
            : throw AppException.BadRequest("worker.emirate_invalid", "Unknown emirate.");
    }

    private static Availability? ParseAvailability(string? value)
    {
        if (value is null)
            return null;
        return Enum.TryParse<Availability>(value, ignoreCase: true, out var availability)
            ? availability
            : throw AppException.BadRequest("worker.availability_invalid", "Unknown availability value.");
    }

    private static void ValidateSalary(int? min, int? max)
    {
        if (min is < 0 or > 1_000_000 || max is < 0 or > 1_000_000)
            throw AppException.BadRequest("worker.salary_invalid", "Salary must be between 0 and 1,000,000 AED.");
        if (min is not null && max is not null && min > max)
            throw AppException.BadRequest("worker.salary_invalid", "Minimum salary cannot exceed maximum salary.");
    }
}
