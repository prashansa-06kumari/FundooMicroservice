using System.Security.Cryptography;
using System.Text;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Auth;

// Password badalte hi stamp badal jaata hai, isliye purana reset token dobara use nahi ho sakta
public static class ResetStamp
{
    public static string For(AppUser user) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash)))[..16];
}