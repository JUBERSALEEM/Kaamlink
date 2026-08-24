using KaamLink.Application.Abstractions;
using KaamLink.Application.Assessments;
using KaamLink.Domain.Assessments;
using KaamLink.Domain.Identity;
using KaamLink.Domain.Workers;

namespace KaamLink.UnitTests;

/// <summary>In-memory assessment repository + a wired SkillAssessmentService harness.</summary>
internal static class AssessmentTestHarness
{
    public static readonly Guid AluminiumSkillId = Guid.NewGuid();
    public static readonly Guid WelderSkillId = Guid.NewGuid();

    public sealed class Context
    {
        public static readonly Guid TemplateId = Guid.NewGuid();
        public static readonly Guid[] QuestionIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        /// <summary>Correct option index per seeded question (in SortOrder): 0, 1, 2.</summary>
        public static readonly int[] CorrectOptions = [0, 1, 2];

        public InMemoryAssessmentRepository Assessments { get; } = new();
        public InMemoryWorkerProfileRepository WorkerProfiles { get; } = new();
        public InMemorySkillCatalogRepository SkillCatalog { get; } = new();
        public InMemoryUserRepository Users { get; } = new();
        public SpyUnitOfWork UnitOfWork { get; } = new();
        public SpyAuditService Audit { get; } = new();

        public FixedTimeProvider Time { get; } =
            new(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));

        public Context()
        {
            SkillCatalog.SeedTestCatalog(AluminiumSkillId, WelderSkillId, Guid.NewGuid());
            SeedTemplate();
        }

        private void SeedTemplate()
        {
            Assessments.Templates.Add(
                SkillAssessment.Create(TemplateId, WelderSkillId, passMarkPercent: 60, Time.GetUtcNow()));
            for (var i = 0; i < QuestionIds.Length; i++)
            {
                Assessments.Questions.Add(AssessmentQuestion.Create(
                    QuestionIds[i], TemplateId,
                    $"Question {i + 1} (EN)", $"سؤال {i + 1} (AR)",
                    $"A{i} en", $"A{i} ar", $"B{i} en", $"B{i} ar",
                    $"C{i} en", $"C{i} ar", $"D{i} en", $"D{i} ar",
                    CorrectOptions[i], i + 1));
            }
        }

        public SkillAssessmentService CreateService() => new(
            Assessments, WorkerProfiles, SkillCatalog, UnitOfWork, Audit, Time);

        /// <summary>Onboarded worker WITH the given skill on their profile.</summary>
        public async Task<(User User, WorkerProfile Profile)> SeedWorkerWithSkillAsync(
            Guid skillId, string phone = "+971501234567")
        {
            var now = Time.GetUtcNow();
            var user = User.Register(phone, isPlatformAdmin: false, now);
            Users.Store.Add(user);

            var profile = WorkerProfile.Create(user.Id, now);
            profile.ApplyBasics("Ahmed Ali", skillId, primarySkillProvided: true,
                KaamLink.Domain.Shared.Emirate.Sharjah, null, "6 years on site.", now);
            profile.ApplyAvailability(Availability.Immediately, 2500, 3500, now);
            profile.RecalculateProgress(hasAnySkill: true, now);
            await WorkerProfiles.AddAsync(profile);

            await WorkerProfiles.ReplaceSkillsAsync(
                profile.Id, [WorkerSkill.Create(profile.Id, skillId, yearsExperience: 5, isPrimary: true)]);
            return (user, profile);
        }
    }

    public static Context Create() => new();
}

internal sealed class InMemoryAssessmentRepository : IAssessmentRepository
{
    public List<SkillAssessment> Templates { get; } = [];
    public List<AssessmentQuestion> Questions { get; } = [];
    public List<AssessmentAttempt> Attempts { get; } = [];

    public Task<SkillAssessment?> FindActiveTemplateBySkillIdAsync(Guid skillId, CancellationToken ct = default) =>
        Task.FromResult(Templates.FirstOrDefault(t => t.SkillId == skillId && t.IsActive));

    public Task<IReadOnlyList<SkillAssessment>> ListActiveTemplatesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SkillAssessment>>(
            Templates.Where(t => t.IsActive).OrderBy(t => t.CreatedAt).ToList());

    public Task<IReadOnlyList<AssessmentQuestion>> ListQuestionsAsync(Guid assessmentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AssessmentQuestion>>(
            Questions.Where(q => q.AssessmentId == assessmentId).OrderBy(q => q.SortOrder).ToList());

    public Task<AssessmentAttempt?> FindAttemptAsync(Guid attemptId, CancellationToken ct = default) =>
        Task.FromResult(Attempts.FirstOrDefault(a => a.Id == attemptId));

    public Task<IReadOnlyList<AssessmentAttempt>> ListAttemptsByWorkerAsync(
        Guid workerProfileId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AssessmentAttempt>>(
            Attempts.Where(a => a.WorkerProfileId == workerProfileId)
                .OrderByDescending(a => a.StartedAt)
                .ToList());

    public Task AddAsync(AssessmentAttempt attempt, CancellationToken ct = default)
    {
        Attempts.Add(attempt);
        return Task.CompletedTask;
    }
}
