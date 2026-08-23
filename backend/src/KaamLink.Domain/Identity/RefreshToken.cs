namespace KaamLink.Domain.Identity;

/// <summary>
/// Rotating refresh token. Only the SHA-256 hash of the opaque token is stored.
/// Rotation chains via <see cref="ReplacedByTokenId"/> enable reuse detection:
/// presenting an already-rotated token means the token leaked.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = default!;

    /// <summary>SHA-256 hex of the opaque token. Never log, never expose.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Set when this token was rotated into a newer one.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAt = now,
        ExpiresAt = now.Add(lifetime)
    };

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && !IsExpired(now);

    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
            return;
        RevokedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }
}
