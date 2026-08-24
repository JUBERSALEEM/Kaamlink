namespace KaamLink.Application.Jobs;

// ---- Shared ----

/// <summary>Cursorless offset page. HasMore is computed from a take+1 fetch — no COUNT(*) query in V1.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);

public sealed record VacancySkillItem(Guid SkillId, string NameEn, string NameAr, bool RequiresCertification);

// ---- Worker-facing ----

public sealed record VacancyFeedItemResponse(
    Guid Id,
    string Title,
    string CompanyName,
    string CompanyVerificationState,
    string Emirate,
    string? Area,
    int? SalaryMinAed,
    int? SalaryMaxAed,
    int Headcount,
    IReadOnlyList<VacancySkillItem> Skills,
    DateTimeOffset CreatedAt);

public sealed record VacancyDetailResponse(
    Guid Id,
    string Title,
    string? Description,
    Guid CompanyId,
    string CompanyName,
    string CompanyVerificationState,
    string Emirate,
    string? Area,
    int? SalaryMinAed,
    int? SalaryMaxAed,
    int Headcount,
    IReadOnlyList<VacancySkillItem> Skills,
    /// <summary>The caller's own application status when they already applied (null = not applied).</summary>
    string? OwnApplicationStatus,
    DateTimeOffset CreatedAt);

public sealed record ApplicationItemResponse(
    Guid Id,
    Guid VacancyId,
    string VacancyTitle,
    string CompanyName,
    string Status,
    string? CoverNote,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt);

public sealed record ApplyRequest
{
    public string? CoverNote { get; init; }
}

// ---- Employer-facing ----

public sealed record CompanyVacancySummaryResponse(
    Guid Id,
    string Title,
    string Status,
    string Emirate,
    string? Area,
    int? SalaryMinAed,
    int? SalaryMaxAed,
    int Headcount,
    int ApplicationCount,
    DateTimeOffset CreatedAt);

public sealed record UpsertVacancyRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Emirate { get; init; } = string.Empty;

    public string? Area { get; init; }

    public int? SalaryMinAed { get; init; }

    public int? SalaryMaxAed { get; init; }

    public int Headcount { get; init; } = 1;

    /// <summary>1..10 catalog skill ids (required trades).</summary>
    public List<Guid> SkillIds { get; init; } = [];
}

public sealed record ChangeVacancyStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

/// <summary>Read-model row for the employer's applicant list (worker profile ⊕ user phone).</summary>
public sealed record ApplicantSnapshot(
    Guid WorkerProfileId,
    Guid UserId,
    string DisplayName,
    string PhoneNumber,
    string? Emirate,
    Guid? PrimarySkillId,
    int ProfileCompletenessPct);

public sealed record VacancyApplicationItemResponse(
    Guid Id,
    string Status,
    string? CoverNote,
    Guid WorkerProfileId,
    string WorkerDisplayName,
    string WorkerPhoneNumber,
    string? WorkerEmirate,
    Guid? WorkerPrimarySkillId,
    int WorkerProfileCompletenessPct,
    DateTimeOffset CreatedAt);

public sealed record ChangeApplicationStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

public sealed record ApplicationStatusChangeResponse(Guid Id, string Status, DateTimeOffset? ReviewedAt);
