using Fundoo.User.Application.Auth;

namespace Fundoo.User.UnitTests;

public class AuthServiceTests
{
    private readonly FakeUserRepository _repo = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(_repo, new FakePasswordHasher(), new FakeTokenService());
    }

    [Fact]
    public async Task Register_NewUser_Succeeds_AndStoresHashAndNormalizedEmail()
    {
        var result = await _service.RegisterAsync("Asha", "Rao", " asha@example.com ", "Str0ng!Passw0rd");

        Assert.True(result.Succeeded);
        var user = Assert.Single(_repo.Users);
        Assert.Equal("ASHA@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("asha@example.com", user.Email);
        Assert.NotEqual("Str0ng!Passw0rd", user.PasswordHash);
    }

    [Fact]
    public async Task Register_SameEmailWithDifferentCapitalization_Fails()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");

        var result = await _service.RegisterAsync("Other", "Person", "ASHA@Example.com", "An0ther!Passw0rd");

        Assert.False(result.Succeeded);
        Assert.Single(_repo.Users);
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsToken()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");

        var result = await _service.LoginAsync("ASHA@example.com", "Str0ng!Passw0rd");

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
    }

    [Fact]
    public async Task Login_UnknownEmail_And_WrongPassword_ReturnSameGenericError()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");

        var unknownEmail = await _service.LoginAsync("nobody@example.com", "Str0ng!Passw0rd");
        var wrongPassword = await _service.LoginAsync("asha@example.com", "wrong-password");

        Assert.False(unknownEmail.Succeeded);
        Assert.False(wrongPassword.Succeeded);
        Assert.Equal(unknownEmail.Error, wrongPassword.Error);
        Assert.Null(unknownEmail.AccessToken);
        Assert.Null(wrongPassword.AccessToken);
    }

    [Fact]
    public async Task Login_InactiveUser_Fails()
    {
        await _service.RegisterAsync("Asha", "Rao", "asha@example.com", "Str0ng!Passw0rd");
        _repo.Users[0].IsActive = false;

        var result = await _service.LoginAsync("asha@example.com", "Str0ng!Passw0rd");

        Assert.False(result.Succeeded);
    }
}