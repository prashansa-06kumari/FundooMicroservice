namespace Fundoo.User.Domain;

public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 2FA Fields
    public bool IsTwoFactorEnabled { get; set; } = false;

    public string? TwoFactorCode { get; set; }

    public DateTime? TwoFactorExpiryUtc { get; set; }

    // SSO / OAuth Fields
    public string AuthProvider { get; set; } = "Local";

    public string? ExternalProviderId { get; set; }

    // Password Reset Fields
    public string? ResetPasswordToken { get; set; }

    public DateTime? ResetPasswordExpiryUtc { get; set; }
}
