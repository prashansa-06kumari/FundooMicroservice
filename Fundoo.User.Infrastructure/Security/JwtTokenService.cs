using System.Security.Claims;
using System.Text;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fundoo.User.Infrastructure.Security;

public class JwtTokenService(JwtOptions options) : ITokenService
{
    public string CreateAccessToken(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            }),
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(options.ExpiryMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}