using KaamLink.Application.Common;
using KaamLink.Application.Workers;

namespace KaamLink.UnitTests.Workers;

public sealed class WorkerProfileServiceTests
{
    private static readonly Guid AluminiumId = Guid.NewGuid();
    private static readonly Guid WelderId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static (AuthServiceHarness.Context Harness, WorkerProfileService Service) Setup()
    {
        var harness = AuthServiceHarness.Create();
        harness.SkillCatalog.SeedTestCatalog(AluminiumId, WelderId, CategoryId);
        var service = new WorkerProfileService(
            harness.WorkerProfiles,
            harness.SkillCatalog,
            harness.UnitOfWork,
            harness.Audit,
            harness.Time);
        return (harness, service);
    }

    [Fact]
    public async Task ProgressiveOnboarding_ReachesComplete_WithExpectedScore()
    {
        var (_, service) = Setup();
        var userId = Guid.NewGuid();

        // Step 1: basics (name + profession + emirate)
        await service.UpdateProfileAsync(userId, new UpdateWorkerProfileRequest
        {
            DisplayName = "Ahmed Ali",
            PrimarySkillId = AluminiumId,
            Emirate = "Sharjah"
        });

        // Step 2: skills
        await service.SetSkillsAsync(userId, new SetWorkerSkillsRequest
        {
            Skills =
            [
                new SetWorkerSkillItem(AluminiumId, 6),
                new SetWorkerSkillItem(WelderId, 3)
            ]
        });

        // Step 3: availability + salary + about
        var done = await service.UpdateProfileAsync(userId, new UpdateWorkerProfileRequest
        {
            Availability = "Immediately",
            ExpectedSalaryMinAed = 2500,
            ExpectedSalaryMaxAed = 3500,
            About = "Fabrication specialist"
        });

        Assert.True(done.OnboardingComplete);
        Assert.Equal(100, done.ProfileCompletenessPct); // 20+20+15+15+15+10+5
        Assert.Equal(2, done.Skills.Count);
        Assert.True(done.Skills.Single(s => s.SkillId == AluminiumId).IsPrimary);
        Assert.Equal("SelfDeclared", done.Skills.First().VerificationState);
        Assert.Equal("فني ألمنيوم", done.Skills.Single(s => s.SkillId == AluminiumId).NameAr);
    }

    [Fact]
    public async Task PartialPatch_KeepsExistingValues_AndReportsIncomplete()
    {
        var (_, service) = Setup();
        var userId = Guid.NewGuid();

        var afterBasics = await service.UpdateProfileAsync(userId, new UpdateWorkerProfileRequest
        {
            DisplayName = "Ravi Kumar",
            Emirate = "Ajman"
        });

        Assert.False(afterBasics.OnboardingComplete);
        Assert.Equal("Ajman", afterBasics.Emirate);
        Assert.Null(afterBasics.PrimarySkillId);

        // A later patch must not wipe earlier steps.
        var afterSalary = await service.UpdateProfileAsync(userId, new UpdateWorkerProfileRequest
        {
            Availability = "WithinTwoWeeks",
            ExpectedSalaryMinAed = 2000,
            ExpectedSalaryMaxAed = 3000
        });

        Assert.Equal("Ravi Kumar", afterSalary.DisplayName);
        Assert.Equal("Ajman", afterSalary.Emirate);
        Assert.False(afterSalary.OnboardingComplete); // still missing profession + skills
    }

    [Fact]
    public async Task SetSkills_UnknownSkill_IsRejected()
    {
        var (_, service) = Setup();

        var exception = await Assert.ThrowsAsync<AppException>(() => service.SetSkillsAsync(
            Guid.NewGuid(),
            new SetWorkerSkillsRequest { Skills = [new SetWorkerSkillItem(Guid.NewGuid(), 1)] }));

        Assert.Equal("worker.skill_unknown", exception.Code);
    }

    [Fact]
    public async Task SalaryRange_IsCrossValidated()
    {
        var (_, service) = Setup();

        var exception = await Assert.ThrowsAsync<AppException>(() => service.UpdateProfileAsync(
            Guid.NewGuid(),
            new UpdateWorkerProfileRequest { ExpectedSalaryMinAed = 9000, ExpectedSalaryMaxAed = 3000 }));

        Assert.Equal("worker.salary_invalid", exception.Code);
    }

    [Fact]
    public async Task GetMine_WithoutProfile_ReturnsNotFoundCode()
    {
        var (_, service) = Setup();

        var exception = await Assert.ThrowsAsync<AppException>(() => service.GetMineAsync(Guid.NewGuid()));

        Assert.Equal("worker.profile_not_found", exception.Code);
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task InvalidEnumValues_AreRejectedWithStableCodes()
    {
        var (_, service) = Setup();
        var userId = Guid.NewGuid();

        var emirate = await Assert.ThrowsAsync<AppException>(() => service.UpdateProfileAsync(
            userId, new UpdateWorkerProfileRequest { Emirate = "Narnia" }));
        Assert.Equal("worker.emirate_invalid", emirate.Code);

        var availability = await Assert.ThrowsAsync<AppException>(() => service.UpdateProfileAsync(
            userId, new UpdateWorkerProfileRequest { Availability = "Whenever" }));
        Assert.Equal("worker.availability_invalid", availability.Code);
    }
}
