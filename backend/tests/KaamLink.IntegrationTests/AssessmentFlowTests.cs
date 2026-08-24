using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KaamLink.Application.Assessments;
using KaamLink.Application.Auth;
using KaamLink.Application.Companies;
using KaamLink.Application.Skills;
using KaamLink.Application.Workers;

namespace KaamLink.IntegrationTests;

/// <summary>
/// End-to-end proof of the Phase 05 Definition of Done over real HTTP + PostgreSQL:
/// worker onboarding with a skill → start (questions WITHOUT correct answers) →
/// server-side scoring → pass writes AssessmentVerified — plus the honesty gates
/// (no template, skill not on profile, onboarding incomplete, single-shot submit).
/// </summary>
public sealed class AssessmentFlowTests : IClassFixture<KaamLinkApiFactory>
{
    private static readonly int[] WelderCorrectOptions = [1, 0, 2, 1, 3]; // seed: WelderQuestions sort order

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly KaamLinkApiFactory _factory;
    private readonly HttpClient _client;

    public AssessmentFlowTests(KaamLinkApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Worker_PassesWelderAssessment_AndSkillBecomesAssessmentVerified()
    {
        var worker = await RegisterAsync(TestPhones.Next());
        var catalog = await GetAsync<SkillCatalogResponse>("/api/v1/skills/catalog", worker.AccessToken);
        var welder = FindSkill(catalog, "Welder");
        await OnboardWorkerWithSkillAsync(worker.AccessToken, welder.Id);

        // Start — capture the RAW payload to prove correct answers never leave the API (§50).
        using var startResponse = await SendAsync(
            HttpMethod.Post, $"/api/v1/workers/me/assessments/{welder.Id}/attempts", worker.AccessToken, body: null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var rawBody = await startResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("correct", rawBody, StringComparison.OrdinalIgnoreCase);

        var started = JsonSerializer.Deserialize<StartAssessmentResponse>(rawBody, Json);
        Assert.NotNull(started);
        Assert.Equal(5, started.TotalQuestions);

        var answers = started.Questions
            .OrderBy(q => q.SortOrder)
            .Select((q, i) => new { questionId = q.Id, selectedOptionIndex = WelderCorrectOptions[i] })
            .ToArray();

        var result = await PostAsync<AssessmentResultResponse>(
            $"/api/v1/workers/me/assessments/{welder.Id}/attempts/{started.AttemptId}/submit",
            worker.AccessToken, new { answers });

        Assert.True(result.Passed);
        Assert.Equal(100, result.ScorePercent);
        Assert.Equal("AssessmentVerified", result.VerificationState);

        // The /me listing reflects the earned state (read-after-write honesty).
        var mine = await GetAsync<List<MyAssessmentItemResponse>>(
            "/api/v1/workers/me/assessments", worker.AccessToken);
        var row = Assert.Single(mine, r => r.SkillId == welder.Id);
        Assert.Equal("AssessmentVerified", row.VerificationState);
        Assert.Equal(100, row.LatestScorePercent);
        Assert.True(row.LatestPassed);
        Assert.True(row.AssessmentAvailable);
    }

    [Fact]
    public async Task Start_ForSkillWithoutTemplate_ReturnsHonestNotAvailable()
    {
        var worker = await RegisterAsync(TestPhones.Next());
        var catalog = await GetAsync<SkillCatalogResponse>("/api/v1/skills/catalog", worker.AccessToken);
        var mason = FindSkill(catalog, "Mason");
        await OnboardWorkerWithSkillAsync(worker.AccessToken, mason.Id);

        using var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/workers/me/assessments/{mason.Id}/attempts", worker.AccessToken, body: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("assessment.not_available", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Start_RequiresTheSkillToBeOnTheProfile()
    {
        var worker = await RegisterAsync(TestPhones.Next());
        var catalog = await GetAsync<SkillCatalogResponse>("/api/v1/skills/catalog", worker.AccessToken);
        var mason = FindSkill(catalog, "Mason");
        var welder = FindSkill(catalog, "Welder");
        await OnboardWorkerWithSkillAsync(worker.AccessToken, mason.Id);

        using var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/workers/me/assessments/{welder.Id}/attempts", worker.AccessToken, body: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("assessment.skill_not_on_profile", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Start_RequiresCompletedOnboarding()
    {
        var worker = await RegisterAsync(TestPhones.Next());
        var catalog = await GetAsync<SkillCatalogResponse>("/api/v1/skills/catalog", worker.AccessToken);
        var welder = FindSkill(catalog, "Welder");

        // No profile at all → profile_required.
        using var noProfile = await SendAsync(
            HttpMethod.Post, $"/api/v1/workers/me/assessments/{welder.Id}/attempts", worker.AccessToken, body: null);
        Assert.Equal("worker.profile_required", await ReadProblemCodeAsync(noProfile));

        // Step 1 only → onboarding_incomplete.
        await PutAsync<WorkerProfileResponse>("/api/v1/workers/me", worker.AccessToken, new
        {
            displayName = "Ahmed Ali",
            primarySkillId = welder.Id,
            emirate = "Sharjah"
        });
        using var incomplete = await SendAsync(
            HttpMethod.Post, $"/api/v1/workers/me/assessments/{welder.Id}/attempts", worker.AccessToken, body: null);
        Assert.Equal("worker.onboarding_incomplete", await ReadProblemCodeAsync(incomplete));
    }

    [Fact]
    public async Task Submit_IsSingleShot()
    {
        var worker = await RegisterAsync(TestPhones.Next());
        var catalog = await GetAsync<SkillCatalogResponse>("/api/v1/skills/catalog", worker.AccessToken);
        var welder = FindSkill(catalog, "Welder");
        await OnboardWorkerWithSkillAsync(worker.AccessToken, welder.Id);

        var started = await PostAsync<StartAssessmentResponse>(
            $"/api/v1/workers/me/assessments/{welder.Id}/attempts", worker.AccessToken, body: null!);
        var answers = started.Questions
            .OrderBy(q => q.SortOrder)
            .Select((q, i) => new { questionId = q.Id, selectedOptionIndex = WelderCorrectOptions[i] })
            .ToArray();

        await PostAsync<AssessmentResultResponse>(
            $"/api/v1/workers/me/assessments/{welder.Id}/attempts/{started.AttemptId}/submit",
            worker.AccessToken, new { answers });

        using var second = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/workers/me/assessments/{welder.Id}/attempts/{started.AttemptId}/submit",
            worker.AccessToken, new { answers });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("assessment.already_completed", await ReadProblemCodeAsync(second));
    }

    // ---- helpers ----

    private async Task OnboardWorkerWithSkillAsync(string accessToken, Guid skillId)
    {
        await PutAsync<WorkerProfileResponse>("/api/v1/workers/me", accessToken, new
        {
            displayName = "Ahmed Ali",
            primarySkillId = skillId,
            emirate = "Sharjah"
        });
        await PutAsync<WorkerProfileResponse>("/api/v1/workers/me/skills", accessToken, new
        {
            skills = new[] { new { skillId, yearsExperience = 5 } }
        });
        await PutAsync<WorkerProfileResponse>("/api/v1/workers/me", accessToken, new
        {
            availability = "Immediately",
            expectedSalaryMinAed = 2500,
            expectedSalaryMaxAed = 3500,
            about = "5 years experience."
        });
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
