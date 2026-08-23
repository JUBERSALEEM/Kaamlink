namespace KaamLink.Domain.Identity;

/// <summary>
/// A one-time passcode challenge. The plaintext code NEVER touches the database —
/// only its HMAC-SHA256 hash (see OtpHasher) is stored.
/// </summary>
public sealed class OtpChallenge
{
    public const int MaxAttempts = 5;

    public Guid Id { get; private set; }

    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>HMAC-SHA256 hex of the code. Never log, never expose.</summary>
    public string CodeHash { get; private set; } = string.Empty;

    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    private OtpChallenge()
    {
    }

    public static OtpChallenge Create(
        string phoneNumber, string codeHash, DateTimeOffset now, TimeSpan lifetime) => new()
    {
        Id = Guid.NewGuid(),
        PhoneNumber = phoneNumber,
        CodeHash = codeHash,
        AttemptCount = 0,
        CreatedAt = now,
        ExpiresAt = now.Add(lifetime)
    };

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsConsumed => ConsumedAt is not null;

    public bool HasAttemptsRemaining => AttemptCount < MaxAttempts;

    public int AttemptsRemaining => MaxAttempts - AttemptCount;

    public void RegisterFailedAttempt() => AttemptCount++;

    public void Consume(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
            throw new InvalidOperationException("OTP challenge was already consumed.");
        ConsumedAt = now;
    }
}
