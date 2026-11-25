namespace Fundoo.User.Api.Models;

public class GoogleLoginRequest
{
    /// <summary>
    /// Google ID Token obtained from Google Sign-In.
    /// (Google provides verified firstName, lastName, and email automatically).
    /// </summary>
    public string? IdToken { get; set; }

    /// <summary>
    /// Email address (Optional for testing in Swagger if IdToken is not provided).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Google unique user ID (Optional).
    /// </summary>
    public string? GoogleId { get; set; }
}
