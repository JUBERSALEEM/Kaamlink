using System.Net.Http.Json;

namespace KaamLink.Core.Api
{
    /// <summary>
    /// Typed access to the KaamLink API. Methods map 1:1 to backend endpoints —
    /// URL segments appear only here. Errors surface as <see cref="ApiException"/>
    /// with the backend's stable problem code; translation to user-facing copy
    /// happens in view models via AppStrings.
    /// </summary>
    public interface IApiClient
    {
        Task<RequestOtpResponseDto> RequestOtpAsync(string phone, CancellationToken ct = default);

        Task<AuthTokensResponseDto> VerifyOtpAsync(string phone, string code, CancellationToken ct = default);

        Task<SkillCatalogResponseDto> GetSkillsCatalogAsync(CancellationToken ct = default);

        /// <summary>Returns null when the caller has no worker profile yet (worker.profile_not_found).</summary>
        Task<WorkerProfileResponseDto?> GetMyWorkerProfileAsync(CancellationToken ct = default);

        Task<WorkerProfileResponseDto> UpdateMyWorkerProfileAsync(
            UpdateWorkerProfileRequestDto request, CancellationToken ct = default);

        Task<WorkerProfileResponseDto> SetMyWorkerSkillsAsync(
            List<SetWorkerSkillItemDto> skills, CancellationToken ct = default);

        Task<CompanySummaryDto> CreateCompanyAsync(CreateCompanyRequestDto request, CancellationToken ct = default);

        Task<IReadOnlyList<CompanySummaryDto>> GetMyCompaniesAsync(CancellationToken ct = default);

        // ---- Jobs (Phase 04) ----

        Task<PagedResponseDto<VacancyFeedItemDto>> SearchVacanciesAsync(
            string? emirate, Guid? skillId, string? keyword, int page, int pageSize, CancellationToken ct = default);

        Task<VacancyDetailDto> GetVacancyAsync(Guid vacancyId, CancellationToken ct = default);

        Task<ApplicationItemDto> ApplyAsync(Guid vacancyId, string? coverNote, CancellationToken ct = default);

        Task<IReadOnlyList<ApplicationItemDto>> GetMyApplicationsAsync(CancellationToken ct = default);

        Task<ApplicationItemDto> WithdrawApplicationAsync(Guid applicationId, CancellationToken ct = default);

        Task<IReadOnlyList<CompanyVacancySummaryDto>> GetCompanyVacanciesAsync(
            Guid companyId, CancellationToken ct = default);

        Task<CompanyVacancySummaryDto> CreateVacancyAsync(
            Guid companyId, UpsertVacancyRequestDto request, CancellationToken ct = default);

        Task<CompanyVacancySummaryDto> UpdateVacancyAsync(
            Guid companyId, Guid vacancyId, UpsertVacancyRequestDto request, CancellationToken ct = default);

        Task<CompanyVacancySummaryDto> ChangeVacancyStatusAsync(
            Guid companyId, Guid vacancyId, string status, CancellationToken ct = default);

        Task<IReadOnlyList<VacancyApplicationItemDto>> GetVacancyApplicationsAsync(
            Guid companyId, Guid vacancyId, CancellationToken ct = default);

        Task<ApplicationStatusChangeDto> ChangeApplicationStatusAsync(
            Guid companyId, Guid vacancyId, Guid applicationId, string status, CancellationToken ct = default);

        // ---- Skill assessments (Phase 05) ----
        // These were implemented on ApiClient but missing from the interface, so
        // every call made through IApiClient failed with CS1061 (net10.0-android).

        Task<IReadOnlyList<MyAssessmentItemDto>> GetMyAssessmentsAsync(CancellationToken ct = default);

        Task<StartAssessmentResponseDto> StartAssessmentAsync(Guid skillId, CancellationToken ct = default);

        Task<AssessmentResultDto> SubmitAssessmentAsync(
            Guid skillId, Guid attemptId, List<SubmitAssessmentAnswerItemDto> answers,
            CancellationToken ct = default);
    }

    public sealed class ApiClient : IApiClient
    {
        // Central error codes the client understands explicitly (see AppStrings keys).
        public static class Codes
        {
            public const string RateLimited = "rate.limit_exceeded";
            public const string ProfileNotFound = "worker.profile_not_found";
            public const string OtpInvalid = "auth.otp_invalid";
            public const string OtpNotFoundOrExpired = "auth.otp_not_found_or_expired";
            public const string OtpLocked = "auth.otp_locked";
            public const string OtpResendCooldown = "auth.otp_resend_cooldown";
            public const string PhoneInvalid = "auth.phone_invalid";
            public const string AlreadyApplied = "jobs.already_applied";
            public const string ProfileRequired = "worker.profile_required";
            public const string OnboardingIncomplete = "worker.onboarding_incomplete";
            public const string CompanyNotVerified = "jobs.company_not_verified";
            public const string SalaryPairRequired = "jobs.salary_pair_required";
            public const string SalaryInvalid = "jobs.salary_invalid";
            public const string AssessmentNotAvailable = "assessment.not_available";
            public const string AssessmentSkillNotOnProfile = "assessment.skill_not_on_profile";
            public const string AssessmentAlreadyCompleted = "assessment.already_completed";
        }

        private readonly HttpClient _http;

        public ApiClient(HttpClient http) => _http = http;

        public Task<RequestOtpResponseDto> RequestOtpAsync(string phone, CancellationToken ct = default) =>
            SendAsync<RequestOtpResponseDto>(HttpMethod.Post, "auth/otp/request",
                new RequestOtpRequestDto(phone), AppJsonContext.Default.RequestOtpRequestDto, AppJsonContext.Default.RequestOtpResponseDto, ct);

        public Task<AuthTokensResponseDto> VerifyOtpAsync(string phone, string code, CancellationToken ct = default) =>
            SendAsync<AuthTokensResponseDto>(HttpMethod.Post, "auth/otp/verify",
                new VerifyOtpRequestDto(phone, code), AppJsonContext.Default.VerifyOtpRequestDto, AppJsonContext.Default.AuthTokensResponseDto, ct);

        public Task<SkillCatalogResponseDto> GetSkillsCatalogAsync(CancellationToken ct = default) =>
            SendAsync<SkillCatalogResponseDto>(HttpMethod.Get, "skills/catalog",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.SkillCatalogResponseDto, ct);

        public async Task<WorkerProfileResponseDto?> GetMyWorkerProfileAsync(CancellationToken ct = default)
        {
            try
            {
                return await SendAsync<WorkerProfileResponseDto>(HttpMethod.Get, "workers/me",
                    body: null, bodyTypeInfo: null, AppJsonContext.Default.WorkerProfileResponseDto, ct);
            }
            catch (ApiException exception) when (exception.Code == Codes.ProfileNotFound)
            {
                return null; // honest "no profile yet" — onboarding will create it
            }
        }

        public Task<WorkerProfileResponseDto> UpdateMyWorkerProfileAsync(
            UpdateWorkerProfileRequestDto request, CancellationToken ct = default) =>
            SendAsync<WorkerProfileResponseDto>(HttpMethod.Put, "workers/me",
                request, AppJsonContext.Default.UpdateWorkerProfileRequestDto, AppJsonContext.Default.WorkerProfileResponseDto, ct);

        public Task<WorkerProfileResponseDto> SetMyWorkerSkillsAsync(
            List<SetWorkerSkillItemDto> skills, CancellationToken ct = default) =>
            SendAsync<WorkerProfileResponseDto>(HttpMethod.Put, "workers/me/skills",
                new SetWorkerSkillsRequestDto(skills), AppJsonContext.Default.SetWorkerSkillsRequestDto, AppJsonContext.Default.WorkerProfileResponseDto, ct);

        public Task<CompanySummaryDto> CreateCompanyAsync(
            CreateCompanyRequestDto request, CancellationToken ct = default) =>
            SendAsync<CompanySummaryDto>(HttpMethod.Post, "companies",
                request, AppJsonContext.Default.CreateCompanyRequestDto, AppJsonContext.Default.CompanySummaryDto, ct);

        public async Task<IReadOnlyList<CompanySummaryDto>> GetMyCompaniesAsync(CancellationToken ct = default) =>
            await SendAsync<IReadOnlyList<CompanySummaryDto>>(HttpMethod.Get, "companies/mine",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.IReadOnlyListCompanySummaryDto, ct)
            ?? Array.Empty<CompanySummaryDto>();

        // ---- Jobs (Phase 04) ----

        public async Task<PagedResponseDto<VacancyFeedItemDto>> SearchVacanciesAsync(
            string? emirate, Guid? skillId, string? keyword, int page, int pageSize, CancellationToken ct = default)
        {
            var parts = new List<string> { $"page={page}", $"pageSize={pageSize}" };
            if (!string.IsNullOrWhiteSpace(emirate))
                parts.Add($"emirate={Uri.EscapeDataString(emirate)}");
            if (skillId is not null)
                parts.Add($"skillId={skillId.Value}");
            if (!string.IsNullOrWhiteSpace(keyword))
                parts.Add($"q={Uri.EscapeDataString(keyword.Trim())}");

            return await SendAsync<PagedResponseDto<VacancyFeedItemDto>>(
                       HttpMethod.Get, "vacancies?" + string.Join("&", parts),
                       body: null, bodyTypeInfo: null, AppJsonContext.Default.PagedResponseDtoVacancyFeedItemDto, ct)
                ?? new PagedResponseDto<VacancyFeedItemDto>(
                    Array.Empty<VacancyFeedItemDto>(), page, pageSize, HasMore: false);
        }

        public Task<VacancyDetailDto> GetVacancyAsync(Guid vacancyId, CancellationToken ct = default) =>
            SendAsync<VacancyDetailDto>(HttpMethod.Get, $"vacancies/{vacancyId}",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.VacancyDetailDto, ct)!;

        public Task<ApplicationItemDto> ApplyAsync(Guid vacancyId, string? coverNote, CancellationToken ct = default) =>
            SendAsync<ApplicationItemDto>(HttpMethod.Post, $"vacancies/{vacancyId}/applications",
                new ApplyRequestDto { CoverNote = coverNote },
                AppJsonContext.Default.ApplyRequestDto, AppJsonContext.Default.ApplicationItemDto, ct)!;

        public async Task<IReadOnlyList<ApplicationItemDto>> GetMyApplicationsAsync(CancellationToken ct = default) =>
            await SendAsync<IReadOnlyList<ApplicationItemDto>>(HttpMethod.Get, "workers/me/applications",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.IReadOnlyListApplicationItemDto, ct)
            ?? Array.Empty<ApplicationItemDto>();

        public Task<ApplicationItemDto> WithdrawApplicationAsync(Guid applicationId, CancellationToken ct = default) =>
            SendAsync<ApplicationItemDto>(HttpMethod.Post, $"workers/me/applications/{applicationId}/withdraw",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.ApplicationItemDto, ct)!;

        // ---- Skill assessments (Phase 05) ----

        public async Task<IReadOnlyList<MyAssessmentItemDto>> GetMyAssessmentsAsync(CancellationToken ct = default) =>
            await SendAsync<IReadOnlyList<MyAssessmentItemDto>>(HttpMethod.Get, "workers/me/assessments",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.IReadOnlyListMyAssessmentItemDto, ct)
            ?? Array.Empty<MyAssessmentItemDto>();

        public Task<StartAssessmentResponseDto> StartAssessmentAsync(Guid skillId, CancellationToken ct = default) =>
            SendAsync<StartAssessmentResponseDto>(HttpMethod.Post, $"workers/me/assessments/{skillId}/attempts",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.StartAssessmentResponseDto, ct)!;

        public Task<AssessmentResultDto> SubmitAssessmentAsync(
            Guid skillId, Guid attemptId, List<SubmitAssessmentAnswerItemDto> answers, CancellationToken ct = default) =>
            SendAsync<AssessmentResultDto>(
                HttpMethod.Post, $"workers/me/assessments/{skillId}/attempts/{attemptId}/submit",
                new SubmitAssessmentRequestDto(answers),
                AppJsonContext.Default.SubmitAssessmentRequestDto, AppJsonContext.Default.AssessmentResultDto, ct)!;

        public async Task<IReadOnlyList<CompanyVacancySummaryDto>> GetCompanyVacanciesAsync(
            Guid companyId, CancellationToken ct = default) =>
            await SendAsync<IReadOnlyList<CompanyVacancySummaryDto>>(
                HttpMethod.Get, $"companies/{companyId}/vacancies",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.IReadOnlyListCompanyVacancySummaryDto, ct)
            ?? Array.Empty<CompanyVacancySummaryDto>();

        public Task<CompanyVacancySummaryDto> CreateVacancyAsync(
            Guid companyId, UpsertVacancyRequestDto request, CancellationToken ct = default) =>
            SendAsync<CompanyVacancySummaryDto>(HttpMethod.Post, $"companies/{companyId}/vacancies",
                request, AppJsonContext.Default.UpsertVacancyRequestDto,
                AppJsonContext.Default.CompanyVacancySummaryDto, ct)!;

        public Task<CompanyVacancySummaryDto> UpdateVacancyAsync(
            Guid companyId, Guid vacancyId, UpsertVacancyRequestDto request, CancellationToken ct = default) =>
            SendAsync<CompanyVacancySummaryDto>(HttpMethod.Put, $"companies/{companyId}/vacancies/{vacancyId}",
                request, AppJsonContext.Default.UpsertVacancyRequestDto,
                AppJsonContext.Default.CompanyVacancySummaryDto, ct)!;

        public Task<CompanyVacancySummaryDto> ChangeVacancyStatusAsync(
            Guid companyId, Guid vacancyId, string status, CancellationToken ct = default) =>
            SendAsync<CompanyVacancySummaryDto>(HttpMethod.Post, $"companies/{companyId}/vacancies/{vacancyId}/status",
                new ChangeVacancyStatusRequestDto(status), AppJsonContext.Default.ChangeVacancyStatusRequestDto,
                AppJsonContext.Default.CompanyVacancySummaryDto, ct)!;

        public async Task<IReadOnlyList<VacancyApplicationItemDto>> GetVacancyApplicationsAsync(
            Guid companyId, Guid vacancyId, CancellationToken ct = default) =>
            await SendAsync<IReadOnlyList<VacancyApplicationItemDto>>(
                HttpMethod.Get, $"companies/{companyId}/vacancies/{vacancyId}/applications",
                body: null, bodyTypeInfo: null, AppJsonContext.Default.IReadOnlyListVacancyApplicationItemDto, ct)
            ?? Array.Empty<VacancyApplicationItemDto>();

        public Task<ApplicationStatusChangeDto> ChangeApplicationStatusAsync(
            Guid companyId, Guid vacancyId, Guid applicationId, string status, CancellationToken ct = default) =>
            SendAsync<ApplicationStatusChangeDto>(HttpMethod.Post,
                $"companies/{companyId}/vacancies/{vacancyId}/applications/{applicationId}/status",
                new ChangeApplicationStatusRequestDto(status), AppJsonContext.Default.ChangeApplicationStatusRequestDto,
                AppJsonContext.Default.ApplicationStatusChangeDto, ct)!;

        // ---- core send pipeline ----

        private async Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string path,
            object? body,
            System.Text.Json.Serialization.Metadata.JsonTypeInfo? bodyTypeInfo,
            System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse> responseTypeInfo,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, ApiConfig.ApiPrefix + path);
            if (body is not null && bodyTypeInfo is not null)
                request.Content = JsonContent.Create(body, bodyTypeInfo);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (HttpRequestException exception)
            {
                throw ApiException.Offline(exception);
            }
            catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
            {
                // HttpClient's own timeout surfaces as TaskCanceledException even
                // though nobody cancelled us. Callers only handle ApiException, so
                // leaving it uncaught crashed the app on slow networks instead of
                // showing the offline message. A genuine caller cancellation is
                // still allowed to propagate.
                throw ApiException.Offline(exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    throw await ToApiExceptionAsync(response, ct);

                try
                {
                    var payload = await response.Content.ReadFromJsonAsync(responseTypeInfo, ct);

                    // A body of literal "null" deserializes to null; every caller
                    // immediately dereferences the result.
                    if (payload is null && !typeof(TResponse).IsValueType)
                        throw new ApiException(
                            $"{typeof(TResponse).Name}: response body was empty.",
                            (int)response.StatusCode,
                            code: null);

                    return payload;
                }
                catch (System.Text.Json.JsonException exception)
                {
                    // A 2xx with an unexpected/empty body (captive portal, proxy,
                    // contract drift) must degrade to a localized error, not crash.
                    throw new ApiException(
                        "The server returned an unexpected response.",
                        (int)response.StatusCode,
                        code: null,
                        exception);
                }
            }
        }

        private static async Task<ApiException> ToApiExceptionAsync(
            HttpResponseMessage response, CancellationToken ct)
        {
            string? code = null;
            string? detail = null;
            try
            {
                var problem = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ProblemDetailsDto, ct);
                code = problem?.Code;
                detail = problem?.Detail;
            }
            catch (System.Text.Json.JsonException)
            {
                // Non-JSON error body (proxy/5xx pages) — fall through with null fields.
            }

            return new ApiException(
                detail ?? $"Request failed with status {(int)response.StatusCode}.",
                (int)response.StatusCode,
                code);
        }
    }
}
