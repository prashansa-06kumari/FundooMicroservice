using Fundoo.User.Application.Auth;

namespace Fundoo.User.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default);
}