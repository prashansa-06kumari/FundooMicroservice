using Fundoo.User.Application.Abstractions;

namespace Fundoo.User.Application.Auth;

public class ForgotPasswordService(
    IUserRepository users,
    IResetTokenService tokens,
    IEmailNotifier notifier,
    IPasswordHasher hasher) : IForgotPasswordService
{
    public async Task RequestAsync(string email, CancellationToken ct = default)
    {
        var user = await users.FindByNormalizedEmailAsync(EmailNormalizer.Normalize(email), ct);

        // Email na mile to bhi chup-chaap return, taaki bahar se pata na chale ki account hai ya nahi
        if (user is null || !user.IsActive) return;

        var token = tokens.CreatePasswordResetToken(user);
        await notifier.SendPasswordResetEmailAsync(user.Email, user.FirstName, token, ct);
    }

    public async Task<AuthResult> ResetAsync(string token, string newPassword, CancellationToken ct = default)
    {
        const string invalid = "Invalid or expired token.";

        var info = await tokens.ValidatePasswordResetTokenAsync(token);
        if (info is null) return AuthResult.Failure(invalid);

        var user = await users.FindByIdAsync(info.UserId, ct);

        // Stamp match nahi hua matlab password pehle hi badal chuka hai, token dobara use nahi ho sakta
        if (user is null || !user.IsActive || info.Stamp != ResetStamp.For(user))
            return AuthResult.Failure(invalid);

        user.PasswordHash = hasher.Hash(newPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await users.SaveChangesAsync(ct);

        return AuthResult.Success();
    }
}