namespace Fundoo.User.Infrastructure.Security;

public class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 30;
    public string SigningKey { get; set; } = "";
}