using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Models;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
