using Fundoo.User.Application.Auth;

namespace Fundoo.User.UnitTests;

public class EmailNormalizerTests
{
    [Theory]
    [InlineData("asha@example.com", "ASHA@EXAMPLE.COM")]
    [InlineData("  Asha@Example.com  ", "ASHA@EXAMPLE.COM")]
    [InlineData("ASHA@EXAMPLE.COM", "ASHA@EXAMPLE.COM")]
    public void Normalize_TrimsAndUppercases(string input, string expected)
    {
        Assert.Equal(expected, EmailNormalizer.Normalize(input));
    }
}