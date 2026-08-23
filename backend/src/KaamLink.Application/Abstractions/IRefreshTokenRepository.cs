using KaamLink.Domain.Identity;

namespace KaamLink.Application.Abstractions;

public interface IRefreshTokenRepository
{
    /// <summary>Finds a token by hash regardless of state (needed for reuse detection).</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);

    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Revokes every unrevoked token of the user (reuse-detection response / global logout).</summary>
    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default);
}
