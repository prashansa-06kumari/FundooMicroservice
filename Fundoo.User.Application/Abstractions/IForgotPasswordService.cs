using Fundoo.User.Application.Auth;

namespace Fundoo.User.Application.Abstractions;

public interface IForgotPasswordService
{
    Task RequestAsync(string email, CancellationToken ct = default);
    Task<AuthResult> ResetAsync(string token, string newPassword, CancellationToken ct = default);
}