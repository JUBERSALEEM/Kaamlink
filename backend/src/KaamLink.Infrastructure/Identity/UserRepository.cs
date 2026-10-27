using KaamLink.Application.Abstractions;
using KaamLink.Domain.Identity;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KaamLink.Infrastructure.Identity;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByPhoneAsync(string phoneNumber, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, ct);

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);
}
