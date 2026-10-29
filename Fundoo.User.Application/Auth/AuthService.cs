using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Auth;

public class AuthService(IUserRepository users, IPasswordHasher hasher, ITokenService tokens) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password, CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(email);

        if (await users.FindByNormalizedEmailAsync(normalized, ct) is not null)
            return AuthResult.Failure("An account with this email already exists.");

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim(),
            NormalizedEmail = normalized,
            PasswordHash = hasher.Hash(password),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);
        return AuthResult.Success();
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await users.FindByNormalizedEmailAsync(EmailNormalizer.Normalize(email), ct);

        // Same generic message for unknown email and wrong password
        if (user is null || !user.IsActive || !hasher.Verify(password, user.PasswordHash))
            return AuthResult.Failure("Invalid email or password.");

        return AuthResult.Success(tokens.CreateAccessToken(user));
    }
}