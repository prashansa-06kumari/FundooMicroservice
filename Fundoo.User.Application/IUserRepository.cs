using Fundoo.User.Domain;

namespace Fundoo.User.Application;

public interface IUserRepository
{
    Task<UserEntity?> GetByEmailAsync(string email);

    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<UserEntity> CreateAsync(UserEntity user);

    Task UpdateUserAsync(UserEntity user, CancellationToken ct = default);

    // Session Management
    Task AddSessionAsync(UserSession session, CancellationToken ct = default);

    Task<UserSession?> GetSessionByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    Task UpdateSessionAsync(UserSession session, CancellationToken ct = default);

    Task<List<UserSession>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task RevokeAllSessionsByUserIdAsync(Guid userId, CancellationToken ct = default);
}
