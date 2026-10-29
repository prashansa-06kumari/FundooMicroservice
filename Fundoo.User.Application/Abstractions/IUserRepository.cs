using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}