using System.Security.Claims;
using System.Text;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.Auth;
using Fundoo.User.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fundoo.User.Infrastructure.Security;

public class JwtResetTokenService(JwtOptions options) : IResetTokenService
{
    // Alag audience: is token se Notes API mein login nahi ho sakta
    public const string ResetAudience = "fundoo-password-reset";
    public const int ExpiryMinutes = 15;

    private SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(options.SigningKey));

    public string CreatePasswordResetToken(AppUser user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("purpose", "password-reset"),
                new Claim("stamp", ResetStamp.For(user))
            }),
            Issuer = options.Issuer,
            Audience = ResetAudience,
            Expires = DateTime.UtcNow.AddMinutes(ExpiryMinutes),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public async Task<ResetTokenInfo?> ValidatePasswordResetTokenAsync(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = ResetAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Key,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt) return null;

        if (!jwt.TryGetPayloadValue<string>("purpose", out var purpose) || purpose != "password-reset") return null;
        if (!jwt.TryGetPayloadValue<string>("stamp", out var stamp) || string.IsNullOrEmpty(stamp)) return null;
        if (!Guid.TryParse(jwt.Subject, out var userId)) return null;

        return new ResetTokenInfo(userId, stamp);
    }
}