using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Contracts;

public class RegisterRequest
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = "";
    [Required, MaxLength(100)] public string LastName { get; set; } = "";
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [Required, MinLength(8), MaxLength(128)] public string Password { get; set; } = "";
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public record TokenResponse(string AccessToken);
public class ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
}

public class ResetPasswordRequest
{
    [Required] public string Token { get; set; } = "";
    [Required, MinLength(8), MaxLength(128)] public string NewPassword { get; set; } = "";
}