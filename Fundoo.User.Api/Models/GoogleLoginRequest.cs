namespace Fundoo.User.Api.Models;

public class GoogleLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? GoogleId { get; set; }
    public string? IdToken { get; set; }
}
