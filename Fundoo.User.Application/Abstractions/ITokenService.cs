using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Abstractions;

public interface ITokenService
{
    string CreateAccessToken(AppUser user);
}