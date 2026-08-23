using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KaamLink.Application.Auth;
using KaamLink.Application.Companies;
using KaamLink.Application.Skills;
using KaamLink.Application.Workers;

namespace KaamLink.IntegrationTests;

/// <summary>
/// End-to-end proof of the Phase 03 Definition of Done, over real HTTP + PostgreSQL:
/// worker progressive onboarding (basics → skills → availability) reaching
/// "onboarding complete", the seeded occupation catalog, employer company onboarding,
/// and the admin-lite verification queue — including negative authorization paths.
/// </summary>
public sealed class OnboardingFlowTests : IClassFixture<KaamLinkApiFactory>
{
    private readonly KaamLinkApiFactory _factory;
    private readonly HttpClient _client;

    public OnboardingFlowTests(KaamLinkApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Worker_OnboardsProgressively_AndMeReflectsCompletion()
    {
        var phone = TestPhones.Next();
        var tokens = await RegisterAsync(phone);

        // A fresh identity has no worker profile yet — /me says so honestly (no fake state).
        Assert.False(tokens.User.Worker.HasProfile);
        Assert.False(tokens.User.Worker.OnboardingComplete);

        var catalog = await GetAsync<SkillCatalogResponse>("/api/v1/skills/catalog", tokens.AccessToken);
        var aluminium = FindSkill(catalog, "Aluminium Fabricator");
        var welder = FindSkill(catalog, "Welder");

        // Step 1 — basics (name + primary profession + emirate): 20 + 20 + 15.
        var step1 = await PutAsync<WorkerProfileResponse>("/api/v1/workers/me", tokens.AccessToken, new
        {
            displayName = "Ahmed Ali",
            primarySkillId = aluminium.Id,
            emirate = "Sharjah"
        });
        Assert.False(step1.OnboardingComplete);
        Assert.Equal(55, step1.ProfileCompletenessPct);

        // Step 2 — self-declared skills (full replacement): +15.
        var step2 = await PutAsync<WorkerProfileResponse>("/api/v1/workers/me/skills", tokens.AccessToken, new
        {
            skills = new[]
            {
                new { skillId = aluminium.Id, yearsExperience = 6 },
                new { skillId = welder.Id, yearsExperience = 3 }
            }
        });
        Assert.Equal(70, step2.ProfileCompletenessPct);
        Assert.Equal(2, step2.Skills.Count);
        Assert.True(step2.Skills.Single(s => s.SkillId == aluminium.Id).IsPrimary);
        // Skills are never shown as verified by default (§50: no fake functionality).
        Assert.All(step2.Skills, s => Assert.Equal("SelfDeclared", s.VerificationState));

        // Step 3 — availability + salary + about: +15 +10 +5 = 100.
        var step3 = await PutAsync<WorkerProfileResponse>("/api/v1/workers/me", tokens.AccessToken, new
        {
            availability = "Immediately",
            expectedSalaryMinAed = 2500,
            expectedSalaryMaxAed = 3500,
            about = "Fabrication specialist, 6 years on site."
        });
        Assert.True(step3.OnboardingComplete);
        Assert.Equal(100, step3.ProfileCompletenessPct);

        // The identity summary used by the app router reflects the finished onboarding.
        var me = await GetAsync<UserSummary>("/api/v1/auth/me", tokens.AccessToken);
        Assert.True(me.Worker.HasProfile);
        Assert.True(me.Worker.OnboardingComplete);
        Assert.Equal(100, me.Worker.ProfileCompletenessPct);
    }

    [Fact]
    public async Task SkillsCatalog_RequiresAuth_AndReturnsSeededOccupations()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/skills/catalog")).StatusCode);

        var tokens = await RegisterAsync(TestPhones.Next());
        var catalog = await GetAsync<SkillCatalogResponse>("/api/v1/skills/catalog", tokens.AccessToken);

        Assert.True(catalog.Categories.Count >= 6);
        Assert.All(catalog.Categories, c => Assert.NotEmpty(c.Skills));

        // The single seeded certification flag — vehicle operation is unambiguous;
        // everything else stays unset rather than inventing UAE legal requirements.
        var driver = FindSkill(catalog, "Light Vehicle Driver");
        Assert.True(driver.RequiresCertification);
        Assert.All(
            catalog.Categories.SelectMany(c => c.Skills).Where(s => s.Id != driver.Id),
            s => Assert.False(s.RequiresCertification));
    }

    [Fact]
    public async Task Employer_RegistersCompany_WhichStartsPending_ThenAdminVerifies()
    {
        var employer = await RegisterAsync(TestPhones.Next());

        var created = await PostAsync<CompanySummaryResponse>("/api/v1/companies", employer.AccessToken, new
        {
            name = "Gulf Fabrication LLC",
            category = "Workshop",
            emirate = "Sharjah",
            area = "Industrial Area 12",
            tradeLicenseNumber = "CN-1234567"
        });
        Assert.Equal("Pending", created.VerificationState);
        Assert.Equal("Owner", created.Role);
        Assert.Null(created.VerifiedAt);

        // The employer's membership view is membership-scoped and carries the company.
        var mine = await GetAsync<List<CompanySummaryResponse>>("/api/v1/companies/mine", employer.AccessToken);
        var entry = Assert.Single(mine);
        Assert.Equal(created.Id, entry.Id);
        Assert.Equal("Pending", entry.VerificationState);

        // /me memberships power the app's role gating.
        var employerMe = await GetAsync<UserSummary>("/api/v1/auth/me", employer.AccessToken);
        Assert.Contains(employerMe.Memberships, m => m.CompanyId == created.Id && m.Role == "Owner");

        // Admin-lite: the bootstrap phone sees the company in the review queue.
        var admin = await RegisterAsync(AdminPhone.Number);
        Assert.True(admin.User.IsPlatformAdmin);

        var queue = await GetAsync<List<CompanySummaryResponse>>("/api/v1/admin/companies/pending", admin.AccessToken);
        Assert.Contains(queue, c => c.Id == created.Id);

        var reviewed = await PostAsync<CompanySummaryResponse>(
            $"/api/v1/admin/companies/{created.Id}/review", admin.AccessToken, new { approve = true });
        Assert.Equal("Verified", reviewed.VerificationState);
        Assert.NotNull(reviewed.VerifiedAt);

        // The employer now sees the verified state — set only by the review, never by the UI.
        var after = await GetAsync<List<CompanySummaryResponse>>("/api/v1/companies/mine", employer.AccessToken);
        Assert.Equal("Verified", Assert.Single(after).VerificationState);
    }

    [Fact]
    public async Task AdminRejection_RequiresReason_AndIsVisibleToEmployer()
    {
        var employer = await RegisterAsync(TestPhones.Next());
        var created = await PostAsync<CompanySummaryResponse>("/api/v1/companies", employer.AccessToken, new
        {
            name = "Desert Star Contracting",
            category = "ConstructionContracting",
            emirate = "Dubai"
        });

        var admin = await RegisterAsync(AdminPhone.Number);

        // Rejection without a reason is a stable problem-details error.
        var noReason = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/companies/{created.Id}/review",
            admin.AccessToken,
            new { approve = false });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal("company.reason_required", await ReadProblemCodeAsync(noReason));

        var rejected = await PostAsync<CompanySummaryResponse>(
            $"/api/v1/admin/companies/{created.Id}/review",
            admin.AccessToken,
            new { approve = false, reason = "Licence document unreadable" });
        Assert.Equal("Rejected", rejected.VerificationState);

        var mine = await GetAsync<List<CompanySummaryResponse>>("/api/v1/companies/mine", employer.AccessToken);
        var entry = Assert.Single(mine);
        Assert.Equal("Rejected", entry.VerificationState);
        Assert.Equal("Licence document unreadable", entry.VerificationReason);

        // The queue no longer lists the reviewed company.
        var queue = await GetAsync<List<CompanySummaryResponse>>("/api/v1/admin/companies/pending", admin.AccessToken);
        Assert.DoesNotContain(queue, c => c.Id == created.Id);
    }

    [Fact]
    public async Task NonAdmin_IsForbidden_FromAdminEndpoints()
    {
        var user = await RegisterAsync(TestPhones.Next());
        Assert.False(user.User.IsPlatformAdmin);

        using var queueRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/companies/pending");
        queueRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(queueRequest)).StatusCode);

        using var reviewRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/companies/{Guid.NewGuid()}/review")
        {
            Content = JsonContent.Create(new { approve = true })
        };
        reviewRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(reviewRequest)).StatusCode);
    }

    [Fact]
    public async Task ProfilesAndCompanies_AreScopedToTheCaller()
    {
        var owner = await RegisterAsync(TestPhones.Next());
        var outsider = await RegisterAsync(TestPhones.Next());

        await PostAsync<CompanySummaryResponse>("/api/v1/companies", owner.AccessToken, new
        {
            name = "Owner Only Metal Works",
            category = "ManufacturingFactory",
            emirate = "Ajman"
        });

        // Another user sees none of the owner's companies.
        var outsiderCompanies = await GetAsync<List<CompanySummaryResponse>>(
            "/api/v1/companies/mine", outsider.AccessToken);
        Assert.Empty(outsiderCompanies);

        // Worker profiles are self-scoped: a fresh user gets the honest 404, not a blank 200.
        var missing = await SendAsync(HttpMethod.Get, "/api/v1/workers/me", outsider.AccessToken, body: null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("worker.profile_not_found", await ReadProblemCodeAsync(missing));
    }

    // ---- helpers ----

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

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string uri, string accessToken, object? body)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static SkillItem FindSkill(SkillCatalogResponse catalog, string nameEn) =>
        catalog.Categories.SelectMany(c => c.Skills).SingleOrDefault(s => s.NameEn == nameEn)
        ?? throw new InvalidOperationException($"Skill '{nameEn}' missing from the seeded catalog.");

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}

/// <summary>The development bootstrap admin (mirrors appsettings.Development.json).</summary>
internal static class AdminPhone
{
    public const string Number = "+971500000000";
}
