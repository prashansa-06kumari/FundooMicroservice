using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Abstractions;

public record ResetTokenInfo(Guid UserId, string Stamp);

public interface IResetTokenService
{
    string CreatePasswordResetToken(AppUser user);
    Task<ResetTokenInfo?> ValidatePasswordResetTokenAsync(string token);
}