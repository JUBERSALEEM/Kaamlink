using KaamLink.Application.Abstractions;
using KaamLink.Domain.Identity;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Identity;

public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await db.RefreshTokens.AddAsync(token, ct);

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default) =>
        db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(r => r.RevokedAt, now),
                ct);
}
