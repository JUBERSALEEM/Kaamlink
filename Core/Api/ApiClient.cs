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
    }

    public sealed class ApiClient : IApiClient
    {
        // Central error codes the client understands explicitly (see AppStrings keys).
        public static class Codes
        {
            public const string RateLimited = "rate.limit_exceeded";
            public const string ProfileNotFound = "worker.profile_not_found";
            public const string OtpInvalid = "auth.otp_invalid";
            public const string PhoneInvalid = "auth.phone_invalid";
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

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    throw await ToApiExceptionAsync(response, ct);

                return await response.Content.ReadFromJsonAsync(responseTypeInfo, ct);
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
