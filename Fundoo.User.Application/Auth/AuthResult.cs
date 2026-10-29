namespace Fundoo.User.Application.Auth;

public record AuthResult(bool Succeeded, string? Error, string? AccessToken)
{
    public static AuthResult Success(string? accessToken = null) => new(true, null, accessToken);
    public static AuthResult Failure(string error) => new(false, error, null);
}