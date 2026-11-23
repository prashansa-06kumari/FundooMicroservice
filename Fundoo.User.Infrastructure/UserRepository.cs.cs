using Fundoo.User.Application;
using Fundoo.User.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.User.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly UserDbContext _context;

    public UserRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task<UserEntity?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<UserEntity> CreateAsync(UserEntity user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task UpdateUserAsync(UserEntity user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddSessionAsync(UserSession session, CancellationToken ct = default)
    {
        await _context.UserSessions.AddAsync(session, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<UserSession?> GetSessionByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        return await _context.UserSessions
            .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken, ct);
    }

    public async Task UpdateSessionAsync(UserSession session, CancellationToken ct = default)
    {
        _context.UserSessions.Update(session);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<UserSession>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task RevokeAllSessionsByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var sessions = await _context.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
        }

        await _context.SaveChangesAsync(ct);
    }
}
