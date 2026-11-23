using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Models;

public class VerifyTwoFactorRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public class ToggleTwoFactorRequest
{
    public bool Enable { get; set; }
}

public class ExternalAuthRequest
{
    [Required]
    public string Provider { get; set; } = "Google";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? ProviderKey { get; set; }
}
