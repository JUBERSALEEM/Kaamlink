using System.Text.Json;
using System.Text.Json.Serialization;

namespace KaamLink.Core.Api
{
    // ---------------------------------------------------------------------
    // Wire DTOs — deliberate mirrors of backend/src/KaamLink.Application contracts.
    // camelCase comes from JsonSerializerDefaults.Web on AppJsonContext.
    // Source-generated (trim/AOT-safe) serialization — required for Android
    // Release trimming; never swap in reflection-based settings.
    // ---------------------------------------------------------------------

    // ---- Auth ----

    public sealed record RequestOtpRequestDto(string Phone);

    public sealed record RequestOtpResponseDto(int ResendCooldownSeconds, int ExpiresInSeconds);

    public sealed record VerifyOtpRequestDto(string Phone, string Code);

    public sealed record RefreshTokenRequestDto(string RefreshToken);

    public sealed record LogoutRequestDto(string RefreshToken);

    /// <summary>POST /auth/v1/otp — request an email OTP (not a link).</summary>
    public sealed record SupabaseOtpRequestDto(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("create_user")] bool CreateUser);

    /// <summary>POST /auth/v1/verify — verify the emailed one-time code.</summary>
    public sealed record SupabaseVerifyOtpRequestDto(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("type")] string Type);

    /// <summary>POST /auth/v1/token?grant_type=password — later password sign-in.</summary>
    public sealed record SupabasePasswordLoginRequestDto(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("password")] string Password);

    /// <summary>PUT /auth/v1/user — set a password after OTP confirmation.</summary>
    public sealed record SupabaseUpdateUserRequestDto(
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("data")] SupabaseUserMetadataDto Data);

    public sealed record SupabaseUserMetadataDto(
        [property: JsonPropertyName("password_set")] bool PasswordSet);

    /// <summary>Common Supabase session payload (snake_case on the wire).</summary>
    public sealed record SupabaseSessionResponseDto(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn);

    /// <summary>POST /api/v1/auth/email/exchange — verified Supabase access token.</summary>
    public sealed record SupabaseExchangeRequestDto(string AccessToken);

    public sealed record AuthTokensResponseDto(
        string TokenType,
        string AccessToken,
        int AccessTokenExpiresIn,
        string RefreshToken,
        int RefreshTokenExpiresIn,
        UserSummaryDto User,
        bool RequiresPasswordSetup = false);

    public sealed record UserSummaryDto(
        Guid Id,
        string PhoneNumber,
        string? DisplayName,
        string? PreferredLocale,
        string Status,
        bool IsPlatformAdmin,
        DateTimeOffset CreatedAt,
        MeWorkerInfoDto Worker,
        IReadOnlyList<MeCompanyMembershipDto> Memberships);

    public sealed record MeWorkerInfoDto(bool HasProfile, bool OnboardingComplete, int ProfileCompletenessPct);

    public sealed record MeCompanyMembershipDto(
        Guid CompanyId, string Name, string Role, string VerificationState);

    /// <summary>RFC 7807 problem details + the API's stable machine-readable code.</summary>
    public sealed record ProblemDetailsDto(string? Title, int? Status, string? Detail, string? Code);

    // ---- Workers ----

    public sealed record WorkerProfileResponseDto(
        Guid Id,
        string DisplayName,
        Guid? PrimarySkillId,
        string? Emirate,
        string? Area,
        string? Availability,
        int? ExpectedSalaryMinAed,
        int? ExpectedSalaryMaxAed,
        string? About,
        int ProfileCompletenessPct,
        bool OnboardingComplete,
        IReadOnlyList<WorkerSkillItemDto> Skills);

    public sealed record WorkerSkillItemDto(
        Guid SkillId,
        string NameEn,
        string NameAr,
        bool RequiresCertification,
        int YearsExperience,
        bool IsPrimary,
        string VerificationState);

    /// <summary>Progressive patch — nulls are omitted from the JSON body (WhenWritingNull).</summary>
    public sealed record UpdateWorkerProfileRequestDto
    {
        public string? DisplayName { get; init; }

        public Guid? PrimarySkillId { get; init; }

        public string? Emirate { get; init; }

        public string? Area { get; init; }

        public string? Availability { get; init; }

        public int? ExpectedSalaryMinAed { get; init; }

        public int? ExpectedSalaryMaxAed { get; init; }

        public string? About { get; init; }
    }

    public sealed record SetWorkerSkillsRequestDto(List<SetWorkerSkillItemDto> Skills);

    public sealed record SetWorkerSkillItemDto(Guid SkillId, int YearsExperience);

    // ---- Companies ----

    public sealed record CompanySummaryDto(
        Guid Id,
        string Name,
        string Category,
        string Emirate,
        string? Area,
        string? TradeLicenseNumber,
        string VerificationState,
        string? VerificationReason,
        DateTimeOffset? VerifiedAt,
        string Role,
        DateTimeOffset CreatedAt);

    public sealed record CreateCompanyRequestDto
    {
        public string Name { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string Emirate { get; init; } = string.Empty;

        public string? Area { get; init; }

        public string? TradeLicenseNumber { get; init; }

        public string? Description { get; init; }
    }

    // ---- Skills catalog ----

    public sealed record SkillCatalogResponseDto(IReadOnlyList<SkillCategoryDto> Categories);

    public sealed record SkillCategoryDto(
        Guid Id, string NameEn, string NameAr, IReadOnlyList<SkillItemDto> Skills);

    public sealed record SkillItemDto(Guid Id, string NameEn, string NameAr, bool RequiresCertification)
    {
        /// <summary>UI-language display name (JSON-ignored; display concern only).</summary>
        [JsonIgnore]
        public string LocalizedName => Presentation.LocalizationHelpers.Localized(NameEn, NameAr);
    }

    // ---- Jobs (Phase 04) ----

    public sealed record PagedResponseDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);

    public sealed record VacancySkillItemDto(Guid SkillId, string NameEn, string NameAr, bool RequiresCertification);

    public sealed record VacancyFeedItemDto(
        Guid Id,
        string Title,
        string CompanyName,
        string CompanyVerificationState,
        string Emirate,
        string? Area,
        int? SalaryMinAed,
        int? SalaryMaxAed,
        int Headcount,
        IReadOnlyList<VacancySkillItemDto> Skills,
        DateTimeOffset CreatedAt);

    public sealed record VacancyDetailDto(
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
        IReadOnlyList<VacancySkillItemDto> Skills,
        string? OwnApplicationStatus,
        DateTimeOffset CreatedAt);

    public sealed record CompanyVacancySummaryDto(
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

    public sealed record UpsertVacancyRequestDto
    {
        public string Title { get; init; } = string.Empty;

        public string? Description { get; init; }

        public string Emirate { get; init; } = string.Empty;

        public string? Area { get; init; }

        public int? SalaryMinAed { get; init; }

        public int? SalaryMaxAed { get; init; }

        public int Headcount { get; init; } = 1;

        public List<Guid> SkillIds { get; init; } = [];
    }

    public sealed record ChangeVacancyStatusRequestDto(string Status);

    public sealed record ApplicationItemDto(
        Guid Id,
        Guid VacancyId,
        string VacancyTitle,
        string CompanyName,
        string Status,
        string? CoverNote,
        DateTimeOffset? ReviewedAt,
        DateTimeOffset CreatedAt);

    public sealed record ApplyRequestDto
    {
        public string? CoverNote { get; init; }
    }

    public sealed record VacancyApplicationItemDto(
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

    public sealed record ChangeApplicationStatusRequestDto(string Status);

    public sealed record ApplicationStatusChangeDto(Guid Id, string Status, DateTimeOffset? ReviewedAt);

    // ---- Skill assessments (Phase 05) ----

    /// <summary>One row of "My assessments" (worker). VerificationState mirrors the server enum name.</summary>
    public sealed record MyAssessmentItemDto(
        Guid SkillId,
        string SkillNameEn,
        string SkillNameAr,
        bool RequiresCertification,
        bool AssessmentAvailable,
        string VerificationState,
        int? LatestScorePercent,
        bool? LatestPassed)
    {
        /// <summary>UI-language display name (JSON-ignored; display concern only).</summary>
        [JsonIgnore]
        public string LocalizedSkillName => Presentation.LocalizationHelpers.Localized(SkillNameEn, SkillNameAr);
    }

    /// <summary>One MCQ question — the contract deliberately carries NO correct-answer field.</summary>
    public sealed record AssessmentQuestionItemDto(
        Guid Id,
        int SortOrder,
        string TextEn,
        string TextAr,
        string OptionAEn,
        string OptionAAr,
        string OptionBEn,
        string OptionBAr,
        string OptionCEn,
        string OptionCAr,
        string OptionDEn,
        string OptionDAr);

    public sealed record StartAssessmentResponseDto(
        Guid AttemptId,
        Guid SkillId,
        int PassMarkPercent,
        int TotalQuestions,
        List<AssessmentQuestionItemDto> Questions);

    public sealed record SubmitAssessmentAnswerItemDto(Guid QuestionId, int SelectedOptionIndex);

    public sealed record SubmitAssessmentRequestDto(List<SubmitAssessmentAnswerItemDto> Answers);

    public sealed record AssessmentResultDto(
        Guid AttemptId,
        int CorrectAnswers,
        int TotalQuestions,
        int ScorePercent,
        bool Passed,
        string VerificationState);

    // ---- Serializer ----

    [JsonSourceGenerationOptions(
        JsonSerializerDefaults.Web,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(RequestOtpRequestDto))]
    [JsonSerializable(typeof(RequestOtpResponseDto))]
    [JsonSerializable(typeof(VerifyOtpRequestDto))]
    [JsonSerializable(typeof(RefreshTokenRequestDto))]
    [JsonSerializable(typeof(LogoutRequestDto))]
    [JsonSerializable(typeof(SupabaseOtpRequestDto))]
    [JsonSerializable(typeof(SupabaseVerifyOtpRequestDto))]
    [JsonSerializable(typeof(SupabasePasswordLoginRequestDto))]
    [JsonSerializable(typeof(SupabaseUpdateUserRequestDto))]
    [JsonSerializable(typeof(SupabaseUserMetadataDto))]
    [JsonSerializable(typeof(SupabaseSessionResponseDto))]
    [JsonSerializable(typeof(SupabaseExchangeRequestDto))]
    [JsonSerializable(typeof(AuthTokensResponseDto))]
    [JsonSerializable(typeof(UserSummaryDto))]
    [JsonSerializable(typeof(ProblemDetailsDto))]
    [JsonSerializable(typeof(WorkerProfileResponseDto))]
    [JsonSerializable(typeof(UpdateWorkerProfileRequestDto))]
    [JsonSerializable(typeof(SetWorkerSkillsRequestDto))]
    [JsonSerializable(typeof(IReadOnlyList<CompanySummaryDto>))]
    [JsonSerializable(typeof(CompanySummaryDto))]
    [JsonSerializable(typeof(CreateCompanyRequestDto))]
    [JsonSerializable(typeof(SkillCatalogResponseDto))]
    [JsonSerializable(typeof(PagedResponseDto<VacancyFeedItemDto>))]
    [JsonSerializable(typeof(VacancyFeedItemDto))]
    [JsonSerializable(typeof(VacancyDetailDto))]
    [JsonSerializable(typeof(CompanyVacancySummaryDto))]
    [JsonSerializable(typeof(IReadOnlyList<CompanyVacancySummaryDto>))]
    [JsonSerializable(typeof(UpsertVacancyRequestDto))]
    [JsonSerializable(typeof(ChangeVacancyStatusRequestDto))]
    [JsonSerializable(typeof(ApplicationItemDto))]
    [JsonSerializable(typeof(IReadOnlyList<ApplicationItemDto>))]
    [JsonSerializable(typeof(ApplyRequestDto))]
    [JsonSerializable(typeof(VacancyApplicationItemDto))]
    [JsonSerializable(typeof(IReadOnlyList<VacancyApplicationItemDto>))]
    [JsonSerializable(typeof(ChangeApplicationStatusRequestDto))]
    [JsonSerializable(typeof(ApplicationStatusChangeDto))]
    [JsonSerializable(typeof(MyAssessmentItemDto))]
    [JsonSerializable(typeof(IReadOnlyList<MyAssessmentItemDto>))]
    [JsonSerializable(typeof(AssessmentQuestionItemDto))]
    [JsonSerializable(typeof(StartAssessmentResponseDto))]
    [JsonSerializable(typeof(SubmitAssessmentRequestDto))]
    [JsonSerializable(typeof(AssessmentResultDto))]
    public sealed partial class AppJsonContext : JsonSerializerContext;
}
