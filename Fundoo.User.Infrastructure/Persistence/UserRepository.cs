using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.User.Infrastructure.Persistence;

public class UserRepository(UserDbContext db) : IUserRepository
{
    public Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}