using Fundoo.User.Infrastructure.Security;

namespace Fundoo.User.UnitTests;

public class PasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_DoesNotReturnThePlainPassword()
    {
        var hash = _hasher.Hash("Str0ng!Passw0rd");
        Assert.NotEqual("Str0ng!Passw0rd", hash);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hash = _hasher.Hash("Str0ng!Passw0rd");
        Assert.True(_hasher.Verify("Str0ng!Passw0rd", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hash = _hasher.Hash("Str0ng!Passw0rd");
        Assert.False(_hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_ProducesDifferentHashes_ForSamePassword()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));   // salt is random
    }
}