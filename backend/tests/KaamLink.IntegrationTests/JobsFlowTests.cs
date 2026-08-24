using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KaamLink.Application.Auth;
using KaamLink.Application.Companies;
using KaamLink.Application.Jobs;
using KaamLink.Application.Skills;
using KaamLink.Application.Workers;

namespace KaamLink.IntegrationTests;

/// <summary>
/// End-to-end proof of the Phase 04 Definition of Done, over real HTTP + PostgreSQL:
/// employer vacancy posting with the verification gate, the worker feed, applying,
/// employer applicant decisions, and withdrawal — plus the negative authorization
/// and scoping paths (anonymous, foreign company, incomplete onboarding).
/// </summary>
public sealed class JobsFlowTests : IClassFixture<KaamLinkApiFactory>
{
    private readonly KaamLinkApiFactory _factory;
    private readonly HttpClient _client;

    public JobsFlowTests(KaamLinkApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task VacancyPublishing_IsGated_ByCompanyVerification_ThenVisibleInFeed()
    {
        var employer = await RegisterAsync(TestPhones.Next());
        var company = await CreateCompanyAsync(employer.AccessToken);

        var created = await PostAsync<CompanyVacancySummaryResponse>(
            $"/api/v1/companies/{company.Id}/vacancies", employer.AccessToken, new
            {
                title = "Aluminium Fabricator",
                emirate = "Sharjah",
                area = "Industrial Area 12",
                salaryMinAed = 3000,
                salaryMaxAed = 4000,
                headcount = 2,
                skillIds = new[] { (await AluminiumSkillIdAsync(employer.AccessToken)) }
            });
        Assert.Equal("Draft", created.Status);

        // Pending company cannot publish.
        var publishDenied = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/companies/{company.Id}/vacancies/{created.Id}/status",
            employer.AccessToken, new { status = "Active" });
        Assert.Equal(HttpStatusCode.BadRequest, publishDenied.StatusCode);
        Assert.Equal("jobs.company_not_verified", await ReadProblemCodeAsync(publishDenied));

        // Platform admin verifies the company (same flow as Phase 03).
        var admin = await RegisterAsync(AdminPhone.Number);
        var review = await PostAsJsonAsync(
            $"/api/v1/admin/companies/{company.Id}/review", admin.AccessToken, new { approve = true });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var activated = await PostAsync<CompanyVacancySummaryResponse>(
            $"/api/v1/companies/{company.Id}/vacancies/{created.Id}/status",
            employer.AccessToken, new { status = "Active" });
        Assert.Equal("Active", activated.Status);

        // Now visible in the worker feed of an unrelated worker.
        var worker = await RegisterAsync(TestPhones.Next());
        var feed = await GetAsync<PagedResponse<VacancyFeedItemResponse>>(
            "/api/v1/vacancies?emirate=Sharjah&page=1&pageSize=10", worker.AccessToken);
        var item = Assert.Single(feed.Items);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal("Verified", item.CompanyVerificationState);
        Assert.False(feed.HasMore);
    }

    [Fact]
    public async Task Worker_Applies_AndEmployerDecides()
    {
        var (employer, company, vacancy) = await PostActiveVacancyAsync();
        var worker = await RegisterAsync(TestPhones.Next());
        await CompleteWorkerOnboardingAsync(worker.AccessToken);

        var applied = await PostAsync<ApplicationItemResponse>(
            $"/api/v1/vacancies/{vacancy.Id}/applications", worker.AccessToken,
            new { coverNote = "6 years of experience, available immediately." });
        Assert.Equal("Applied", applied.Status);
        Assert.Equal(vacancy.Title, applied.VacancyTitle);

        // One application per worker per vacancy.
        var duplicate = await SendAsync(
            HttpMethod.Post, $"/api/v1/vacancies/{vacancy.Id}/applications", worker.AccessToken, new { });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal("jobs.already_applied", await ReadProblemCodeAsync(duplicate));

        // The vacancy detail now carries the worker's own status.
        var detail = await GetAsync<VacancyDetailResponse>(
            $"/api/v1/vacancies/{vacancy.Id}", worker.AccessToken);
        Assert.Equal("Applied", detail.OwnApplicationStatus);

        // Employer sees the applicant (with phone — direct hiring) and shortlists.
        var applicants = await GetAsync<List<VacancyApplicationItemResponse>>(
            $"/api/v1/companies/{company.Id}/vacancies/{vacancy.Id}/applications", employer.AccessToken);
        var applicant = Assert.Single(applicants);
        Assert.Equal(applied.Id, applicant.Id);
        Assert.NotEmpty(applicant.WorkerPhoneNumber);

        await PostAsync<ApplicationStatusChangeResponse>(
            $"/api/v1/companies/{company.Id}/vacancies/{vacancy.Id}/applications/{applied.Id}/status",
            employer.AccessToken, new { status = "Shortlisted" });

        var mine = await GetAsync<List<ApplicationItemResponse>>(
            "/api/v1/workers/me/applications", worker.AccessToken);
        Assert.Equal("Shortlisted", Assert.Single(mine).Status);
    }

    [Fact]
    public async Task Apply_RequiresCompletedOnboarding()
    {
        var (_, _, vacancy) = await PostActiveVacancyAsync();
        var freshWorker = await RegisterAsync(TestPhones.Next()); // no onboarding at all

        var denied = await SendAsync(
            HttpMethod.Post, $"/api/v1/vacancies/{vacancy.Id}/applications", freshWorker.AccessToken, new { });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal("worker.profile_required", await ReadProblemCodeAsync(denied));
    }

    [Fact]
    public async Task Endpoints_RejectAnonymous_AndForeignCompany()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/vacancies")).StatusCode);

        var (employer, company, vacancy) = await PostActiveVacancyAsync();
        var outsider = await RegisterAsync(TestPhones.Next());

        // A non-member cannot see or manage another company's vacancies (404, no existence leak).
        var listDenied = await SendAsync(
            HttpMethod.Get, $"/api/v1/companies/{company.Id}/vacancies", outsider.AccessToken, body: null);
        Assert.Equal(HttpStatusCode.NotFound, listDenied.StatusCode);

        var rushDenied = await SendAsync(
            HttpMethod.Put, $"/api/v1/companies/{company.Id}/vacancies/{vacancy.Id}", outsider.AccessToken,
            new { title = "Takeover", emirate = "Dubai", skillIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.NotFound, rushDenied.StatusCode);
        _ = employer;
    }

    [Fact]
    public async Task Withdraw_ClosesTheApplication_ForBothSides()
    {
        var (employer, company, vacancy) = await PostActiveVacancyAsync();
        var worker = await RegisterAsync(TestPhones.Next());
        await CompleteWorkerOnboardingAsync(worker.AccessToken);

        var applied = await PostAsync<ApplicationItemResponse>(
            $"/api/v1/vacancies/{vacancy.Id}/applications", worker.AccessToken, new { });

        var withdrawn = await PostAsync<ApplicationItemResponse>(
            $"/api/v1/workers/me/applications/{applied.Id}/withdraw", worker.AccessToken, new { });
        Assert.Equal("Withdrawn", withdrawn.Status);

        // Terminal for the employer too.
        var denied = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/companies/{company.Id}/vacancies/{vacancy.Id}/applications/{applied.Id}/status",
            employer.AccessToken, new { status = "Shortlisted" });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal("jobs.application_terminal", await ReadProblemCodeAsync(denied));
    }

    // ---- helpers ----

    /// <summary>Registers an employer, verifies the company via the platform admin, posts one active vacancy.</summary>
    private async Task<(AuthTokensResponse Employer, CompanySummaryResponse Company, CompanyVacancySummaryResponse Vacancy)>
        PostActiveVacancyAsync()
    {
        var employer = await RegisterAsync(TestPhones.Next());
        var company = await CreateCompanyAsync(employer.AccessToken);

        var admin = await RegisterAsync(AdminPhone.Number);
        var review = await PostAsJsonAsync(
            $"/api/v1/admin/companies/{company.Id}/review", admin.AccessToken, new { approve = true });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var created = await PostAsync<CompanyVacancySummaryResponse>(
            $"/api/v1/companies/{company.Id}/vacancies", employer.AccessToken, new
            {
                title = "Aluminium Fabricator",
                emirate = "Sharjah",
                salaryMinAed = 3000,
                salaryMaxAed = 4000,
                headcount = 2,
                skillIds = new[] { (await AluminiumSkillIdAsync(employer.AccessToken)) }
            });
        await PostAsync<CompanyVacancySummaryResponse>(
            $"/api/v1/companies/{company.Id}/vacancies/{created.Id}/status",
            employer.AccessToken, new { status = "Active" });
        return (employer, company, created with { Status = "Active" });
    }

    private async Task<CompanySummaryResponse> CreateCompanyAsync(string accessToken) =>
        await PostAsync<CompanySummaryResponse>("/api/v1/companies", accessToken, new
        {
            name = "Gulf Fabrication LLC",
            category = "Workshop",
            emirate = "Sharjah",
            area = "Industrial Area 12",
            tradeLicenseNumber = "CN-1234567"
        });

    private async Task<Guid> AluminiumSkillIdAsync(string accessToken)
    {
        var catalog = await GetAsync<SkillCatalogResponse>("/api/v1/skills/catalog", accessToken);
        return catalog.Categories.SelectMany(c => c.Skills)
            .Single(s => s.NameEn == "Aluminium Fabricator").Id;
    }

    private async Task CompleteWorkerOnboardingAsync(string accessToken)
    {
        var skillId = await AluminiumSkillIdAsync(accessToken);
        await PutAsync<WorkerProfileResponse>("/api/v1/workers/me", accessToken, new
        {
            displayName = "Ahmed Ali",
            primarySkillId = skillId,
            emirate = "Sharjah"
        });
        await PutAsync<WorkerProfileResponse>("/api/v1/workers/me/skills", accessToken, new
        {
            skills = new[] { new { skillId, yearsExperience = 6 } }
        });
        var done = await PutAsync<WorkerProfileResponse>("/api/v1/workers/me", accessToken, new
        {
            availability = "Immediately"
        });
        Assert.True(done.OnboardingComplete);
    }

    private async Task<AuthTokensResponse> RegisterAsync(string phone)
    {
        var requestResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone });
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify", new { phone, code = _factory.OtpSender.GetCodeFor(phone) });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var tokens = await verifyResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        Assert.NotNull(tokens);
        return tokens;
    }

    private async Task<T> GetAsync<T>(string uri, string accessToken)
    {
        using var response = await SendAsync(HttpMethod.Get, uri, accessToken, body: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(payload);
        return payload;
    }

    private async Task<T> PutAsync<T>(string uri, string accessToken, object body)
    {
        using var response = await SendAsync(HttpMethod.Put, uri, accessToken, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(payload);
        return payload;
    }

    private async Task<T> PostAsync<T>(string uri, string accessToken, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, uri, accessToken, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(payload);
        return payload;
    }

    private async Task<HttpResponseMessage> PostAsJsonAsync(string uri, string accessToken, object body) =>
        await SendAsync(HttpMethod.Post, uri, accessToken, body);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string uri, string accessToken, object? body)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
