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

    public sealed record AuthTokensResponseDto(
        string TokenType,
        string AccessToken,
        int AccessTokenExpiresIn,
        string RefreshToken,
        int RefreshTokenExpiresIn,
        UserSummaryDto User);

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

    public sealed record SkillItemDto(Guid Id, string NameEn, string NameAr, bool RequiresCertification);

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
    public sealed partial class AppJsonContext : JsonSerializerContext;
}
