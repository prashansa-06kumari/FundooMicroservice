using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.UnitTests;

public class FakeUserRepository : IUserRepository
{
    public List<AppUser> Users { get; } = new();

    public Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));

    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;
    public bool Verify(string password, string hash) => hash == "hashed:" + password;
}

public class FakeTokenService : ITokenService
{
    public string CreateAccessToken(AppUser user) => "token-for-" + user.Id;
}