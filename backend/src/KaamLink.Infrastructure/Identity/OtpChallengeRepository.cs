using KaamLink.Application.Abstractions;
using KaamLink.Domain.Identity;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Identity;

public sealed class OtpChallengeRepository(AppDbContext db) : IOtpChallengeRepository
{
    public Task<OtpChallenge?> FindLatestByPhoneAsync(string phoneNumber, CancellationToken ct = default) =>
        db.OtpChallenges
            .Where(o => o.PhoneNumber == phoneNumber)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<OtpChallenge?> FindLatestActiveByPhoneAsync(
        string phoneNumber, DateTimeOffset now, CancellationToken ct = default) =>
        db.OtpChallenges
            .Where(o => o.PhoneNumber == phoneNumber && o.ConsumedAt == null && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(OtpChallenge challenge, CancellationToken ct = default) =>
        await db.OtpChallenges.AddAsync(challenge, ct);
}
