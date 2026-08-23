using KaamLink.Domain.Identity;

namespace KaamLink.Application.Abstractions;

public interface IOtpChallengeRepository
{
    /// <summary>Most recent challenge for the phone number, any state (used for resend-cooldown checks).</summary>
    Task<OtpChallenge?> FindLatestByPhoneAsync(string phoneNumber, CancellationToken ct = default);

    /// <summary>Most recent challenge that is unconsumed and unexpired at <paramref name="now"/>.</summary>
    Task<OtpChallenge?> FindLatestActiveByPhoneAsync(string phoneNumber, DateTimeOffset now, CancellationToken ct = default);

    Task AddAsync(OtpChallenge challenge, CancellationToken ct = default);
}
