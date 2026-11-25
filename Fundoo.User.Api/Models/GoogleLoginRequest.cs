namespace Fundoo.User.Api.Models;

public class GoogleLoginRequest
{
    /// <summary>
    /// Google ID Token from Google Cloud Console / Google Sign-In.
    /// (Optional for direct testing; if provided, verified directly with Google).
    /// </summary>
    public string? IdToken { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    /// <summary>
    /// Google unique user ID (Optional, e.g. "google_123456").
    /// </summary>
    public string? GoogleId { get; set; }

    /// <summary>
    /// Optional full name fallback.
    /// </summary>
    public string? FullName { get; set; }
}
