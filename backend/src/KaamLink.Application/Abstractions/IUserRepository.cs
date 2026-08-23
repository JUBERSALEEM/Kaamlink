using KaamLink.Domain.Identity;

namespace KaamLink.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task<User?> FindByPhoneAsync(string phoneNumber, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);
}
