using KaamLink.Application.Abstractions;
using KaamLink.Application.Auth;
using KaamLink.Application.Options;
using KaamLink.Domain.Identity;

namespace KaamLink.UnitTests;

/// <summary>In-memory fakes + a factory producing a fully wired AuthService harness.</summary>
internal static class AuthServiceHarness
{
    public sealed class Context
    {
        public InMemoryUserRepository Users { get; } = new();
        public InMemoryOtpChallengeRepository OtpChallenges { get; } = new();
        public InMemoryRefreshTokenRepository RefreshTokens { get; } = new();
        public SpyUnitOfWork UnitOfWork { get; } = new();
        public SpyOtpSender OtpSender { get; } = new();
        public StubTokenService TokenService { get; } = new();
        public SpyAuditService Audit { get; } = new();

        public FixedTimeProvider Time { get; } =
            new(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));

        public OtpOptions OtpOptions { get; } = new()
        {
            Provider = "log",
            HashingKey = "unit-test-hashing-key-12345",
            ChallengeLifetimeSeconds = 300,
            ResendCooldownSeconds = 60
        };

        public JwtOptions JwtOptions { get; } = new()
        {
            Issuer = "kaamlink-api",
            Audience = "kaamlink-app",
            SigningKey = "unit-test-signing-key-0123456789abcdef",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30
        };

        public AdminOptions AdminOptions { get; } = new() { BootstrapPhones = ["+971500000000"] };

        public InMemoryWorkerProfileRepository WorkerProfiles { get; } = new();

        public InMemoryCompanyRepository Companies { get; } = new();

        public InMemorySkillCatalogRepository SkillCatalog { get; } = new();

        public AuthService CreateService() => new(
            Users,
            OtpChallenges,
            RefreshTokens,
            UnitOfWork,
            OtpSender,
            TokenService,
            Audit,
            OtpOptions,
            JwtOptions,
            Time,
            AdminOptions,
            WorkerProfiles,
            Companies);

        /// <summary>Requests an OTP and returns the code the sender captured.</summary>
        public async Task<string> RequestOtpAndCaptureCodeAsync(string phone)
        {
            var service = CreateService();
            await service.RequestOtpAsync(new RequestOtpRequest { Phone = phone });
            return OtpSender.LastCode ?? throw new InvalidOperationException("No OTP was sent.");
        }
    }

    public static Context Create() => new();
}

internal sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Store { get; } = [];

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(u => u.Id == id));

    public Task<User?> FindByPhoneAsync(string phoneNumber, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(u => u.PhoneNumber == phoneNumber));

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        Store.Add(user);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryOtpChallengeRepository : IOtpChallengeRepository
{
    public List<OtpChallenge> Store { get; } = [];

    public Task<OtpChallenge?> FindLatestByPhoneAsync(string phoneNumber, CancellationToken ct = default) =>
        Task.FromResult(Store
            .Where(o => o.PhoneNumber == phoneNumber)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault());

    public Task<OtpChallenge?> FindLatestActiveByPhoneAsync(
        string phoneNumber, DateTimeOffset now, CancellationToken ct = default) =>
        Task.FromResult(Store
            .Where(o => o.PhoneNumber == phoneNumber && !o.IsConsumed && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault());

    public Task AddAsync(OtpChallenge challenge, CancellationToken ct = default)
    {
        Store.Add(challenge);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Store { get; } = [];

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(r => r.TokenHash == tokenHash));

    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        Store.Add(token);
        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var token in Store.Where(r => r.UserId == userId && r.RevokedAt is null))
            token.Revoke(now);

        return Task.CompletedTask;
    }
}

internal sealed class SpyUnitOfWork : IUnitOfWork
{
    public int SaveCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCalls++;
        return Task.FromResult(1);
    }
}

internal sealed class SpyOtpSender : IOtpSender
{
    public List<(string PhoneNumber, string Code)> Sent { get; } = [];

    public string? LastCode => Sent.Count > 0 ? Sent[^1].Code : null;

    public Task SendAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        Sent.Add((phoneNumber, code));
        return Task.CompletedTask;
    }
}

internal sealed class StubTokenService : ITokenService
{
    private int _refreshCounter;

    public AccessTokenIssue IssueAccessToken(User user) => new($"access-token-for-{user.Id}", 900);

    public string NewOpaqueToken() => $"refresh-token-{++_refreshCounter}";
}

internal sealed record SpyAuditRecord(
    string Action,
    string EntityType,
    string? EntityId,
    Guid? ActorUserId,
    string? MetadataJson);

internal sealed class SpyAuditService : IAuditService
{
    public List<SpyAuditRecord> Records { get; } = [];

    public Task RecordAsync(
        string action,
        string entityType,
        string? entityId = null,
        Guid? actorUserId = null,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        Records.Add(new SpyAuditRecord(action, entityType, entityId, actorUserId, metadataJson));
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    public FixedTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;

    public DateTimeOffset UtcNow { get; private set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);

    public void Set(DateTimeOffset value) => UtcNow = value;
}

// ---- Phase 03 fakes ----

internal sealed class InMemoryWorkerProfileRepository : IWorkerProfileRepository
{
    public List<KaamLink.Domain.Workers.WorkerProfile> Store { get; } = [];
    public List<KaamLink.Domain.Workers.WorkerSkill> Skills { get; } = [];

    public Task<KaamLink.Domain.Workers.WorkerProfile?> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(w => w.UserId == userId));

    public Task AddAsync(KaamLink.Domain.Workers.WorkerProfile profile, CancellationToken ct = default)
    {
        Store.Add(profile);
        return Task.CompletedTask;
    }

    public Task ReplaceSkillsAsync(
        Guid workerProfileId, IReadOnlyList<KaamLink.Domain.Workers.WorkerSkill> skills, CancellationToken ct = default)
    {
        Skills.RemoveAll(s => s.WorkerProfileId == workerProfileId);
        Skills.AddRange(skills);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KaamLink.Domain.Workers.WorkerSkill>> ListSkillsAsync(
        Guid workerProfileId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KaamLink.Domain.Workers.WorkerSkill>>(
            Skills.Where(s => s.WorkerProfileId == workerProfileId).ToList());
}

internal sealed class InMemoryCompanyRepository : ICompanyRepository
{
    public List<KaamLink.Domain.Companies.Company> Companies { get; } = [];
    public List<KaamLink.Domain.Companies.CompanyMember> Members { get; } = [];

    public Task AddCompanyAsync(KaamLink.Domain.Companies.Company company, CancellationToken ct = default)
    {
        Companies.Add(company);
        return Task.CompletedTask;
    }

    public Task AddMemberAsync(KaamLink.Domain.Companies.CompanyMember member, CancellationToken ct = default)
    {
        Members.Add(member);
        return Task.CompletedTask;
    }

    public Task<KaamLink.Domain.Companies.Company?> FindByIdAsync(Guid companyId, CancellationToken ct = default) =>
        Task.FromResult(Companies.FirstOrDefault(c => c.Id == companyId));

    public Task<IReadOnlyList<KaamLink.Domain.Companies.CompanyMember>> FindMembershipsByUserIdAsync(
        Guid userId, CancellationToken ct = default)
    {
        // Mimic EF Include(m => m.Company): return members with the nav populated.
        var result = Members
            .Where(m => m.UserId == userId)
            .Select(m => KaamLink.Domain.Companies.CompanyMember.Create(
                m.CompanyId,
                m.UserId,
                m.Role,
                m.CreatedAt,
                Companies.Single(c => c.Id == m.CompanyId)))
            .ToList();

        return Task.FromResult<IReadOnlyList<KaamLink.Domain.Companies.CompanyMember>>(result);
    }

    public Task<KaamLink.Domain.Companies.CompanyMember?> FindMembershipAsync(
        Guid companyId, Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Members.FirstOrDefault(m => m.CompanyId == companyId && m.UserId == userId));

    public Task<IReadOnlyList<KaamLink.Domain.Companies.Company>> ListPendingReviewAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KaamLink.Domain.Companies.Company>>(
            Companies
                .Where(c => c.VerificationState == KaamLink.Domain.Companies.CompanyVerificationState.Pending)
                .OrderBy(c => c.CreatedAt)
                .ToList());

    public Task<int> CountOwnedCompaniesAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Members.Count(
            m => m.UserId == userId && m.Role == KaamLink.Domain.Companies.CompanyRole.Owner));
}

internal sealed class InMemorySkillCatalogRepository : ISkillCatalogRepository
{
    public List<KaamLink.Domain.Skills.SkillCategory> Categories { get; } = [];

    public List<KaamLink.Domain.Skills.Skill> Skills { get; } = [];

    /// <summary>
    /// Seeds a minimal catalog mirroring the real seed names, so tests can assert
    /// localization (e.g. فني ألمنيوم) without depending on fixed production GUIDs.
    /// </summary>
    public void SeedTestCatalog(Guid aluminiumSkillId, Guid welderSkillId, Guid categoryId)
    {
        var category = KaamLink.Domain.Skills.SkillCategory.Create(
            categoryId, "Fabrication & Welding", "التصنيع واللحام", 1);
        var aluminium = KaamLink.Domain.Skills.Skill.Create(
            aluminiumSkillId, categoryId, "Aluminium Fabricator", "فني ألمنيوم", false, 1);
        var welder = KaamLink.Domain.Skills.Skill.Create(
            welderSkillId, categoryId, "Welder", "لحّام", false, 2);
        category.Skills.Add(aluminium);
        category.Skills.Add(welder);
        Categories.Add(category);
        Skills.Add(aluminium);
        Skills.Add(welder);
    }

    public Task<IReadOnlyList<KaamLink.Domain.Skills.SkillCategory>> ListCategoriesWithSkillsAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KaamLink.Domain.Skills.SkillCategory>>(
            Categories.OrderBy(c => c.SortOrder).ToList());

    public Task<KaamLink.Domain.Skills.Skill?> FindSkillAsync(Guid skillId, CancellationToken ct = default) =>
        Task.FromResult(Skills.FirstOrDefault(s => s.Id == skillId));

    public Task<bool> SkillExistsAsync(Guid skillId, CancellationToken ct = default) =>
        Task.FromResult(Skills.Any(s => s.Id == skillId));
}

