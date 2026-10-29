using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, IForgotPasswordService forgotPassword) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request.FirstName, request.LastName, request.Email, request.Password, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: result.Error);

        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.Email, request.Password, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);

        return Ok(new TokenResponse(result.AccessToken!));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await forgotPassword.RequestAsync(request.Email, ct);

        // Email exist kare ya na kare, jawab hamesha same
        return Ok(new { message = "If an account exists for this email, a reset token has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await forgotPassword.ResetAsync(request.Token, request.NewPassword, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);

        return Ok(new { message = "Password has been reset. You can now log in." });
    }
}