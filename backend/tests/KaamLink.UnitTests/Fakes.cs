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
            Time);

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
