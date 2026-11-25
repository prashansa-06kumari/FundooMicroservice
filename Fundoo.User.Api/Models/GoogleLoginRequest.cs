namespace Fundoo.User.Api.Models;

public class GoogleLoginRequest
{
    /// <summary>
    /// Google ID Token obtained from Google Sign-In (Google Cloud Console OAuth 2.0).
    /// If provided, backend verifies it cryptographically with Google's servers.
    /// </summary>
    public string? IdToken { get; set; }

    /// <summary>
    /// User email address (auto-extracted from IdToken if IdToken is passed).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// User full name (auto-extracted from IdToken if IdToken is passed).
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// Unique Google Subject ID (auto-extracted from IdToken if IdToken is passed).
    /// </summary>
    public string? GoogleId { get; set; }
}
