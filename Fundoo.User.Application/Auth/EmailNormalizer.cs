namespace Fundoo.User.Application.Auth;

public static class EmailNormalizer
{
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
