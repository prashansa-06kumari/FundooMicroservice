using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fundoo.User.Api.Models;
using Fundoo.User.Application;
using Fundoo.User.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher<UserEntity> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IEmailService _emailService;

    public UsersController(
        IUserRepository repository,
        IPasswordHasher<UserEntity> passwordHasher,
        IConfiguration configuration,
        IKafkaProducer kafkaProducer,
        IEmailService emailService)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _kafkaProducer = kafkaProducer;
        _emailService = emailService;
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? User.FindFirstValue("userId");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _repository.GetByEmailAsync(email);

        if (existingUser != null)
        {
            return Conflict(new
            {
                message = "Email already registered."
            });
        }

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash =
            _passwordHasher.HashPassword(user, request.Password);

        await _repository.CreateAsync(user);

        // Publish event to Kafka for Notification Service
        var eventPayload = JsonSerializer.Serialize(new
        {
            eventType = "UserRegistered",
            userId = user.Id,
            email = user.Email,
            fullName = user.FullName,
            timestamp = DateTime.UtcNow
        });
        await _kafkaProducer.ProduceAsync("user-events", user.Id.ToString(), eventPayload);

        return Ok(new
        {
            message = "User registered successfully!",
            user.Id,
            user.FullName,
            user.Email
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _repository.GetByEmailAsync(email);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // Check if 2FA is enabled
        if (user.IsTwoFactorEnabled)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            user.TwoFactorCode = code;
            user.TwoFactorExpiryUtc = DateTime.UtcNow.AddMinutes(10);
            await _repository.UpdateUserAsync(user);

            // Send 2FA code via Kafka
            var twoFactorEvent = JsonSerializer.Serialize(new
            {
                eventType = "TwoFactorRequested",
                userId = user.Id,
                email = user.Email,
                fullName = user.FullName,
                payload = code,
                timestamp = DateTime.UtcNow
            });
            await _kafkaProducer.ProduceAsync("user-events", user.Id.ToString(), twoFactorEvent);

            // Direct email fallback
            await _emailService.SendEmailAsync(
                user.Email,
                "Fundoo Notes - Two-Factor Authentication Code",
                $"Hello {user.FullName},\n\nYour 2FA verification code is: {code}\nThis code will expire in 10 minutes.");

            return Ok(new
            {
                requiresTwoFactor = true,
                message = "Two-factor verification code has been sent to your email."
            });
        }

        return await GenerateAuthResponseAsync(user);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.GetByEmailAsync(email);

        if (user == null)
        {
            return Ok(new
            {
                message = "If this email is registered, a password reset link/token has been sent."
            });
        }

        // Generate 32-byte secure reset token
        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.ResetPasswordToken = resetToken;
        user.ResetPasswordExpiryUtc = DateTime.UtcNow.AddMinutes(15);
        await _repository.UpdateUserAsync(user);

        // Send Kafka event for password reset notification
        var resetEvent = JsonSerializer.Serialize(new
        {
            eventType = "ForgotPasswordRequested",
            userId = user.Id,
            email = user.Email,
            fullName = user.FullName,
            payload = resetToken,
            timestamp = DateTime.UtcNow
        });
        await _kafkaProducer.ProduceAsync("user-events", user.Id.ToString(), resetEvent);

        // Direct email delivery fallback
        await _emailService.SendEmailAsync(
            user.Email,
            "Fundoo Notes - Reset Your Password",
            $"Hello {user.FullName},\n\nYou requested to reset your password for your Fundoo Notes account.\n\nYour Password Reset Token is:\n{resetToken}\n\nPlease use this token in the Reset Password form to set your new password. This token will expire in 15 minutes.\n\nIf you did not request this, please ignore this email.");

        return Ok(new
        {
            message = "Password reset instructions have been sent to your email."
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.GetByEmailAsync(email);

        if (user == null)
        {
            return BadRequest(new { message = "Invalid email or reset token." });
        }

        if (string.IsNullOrWhiteSpace(user.ResetPasswordToken) ||
            user.ResetPasswordToken != request.Token.Trim() ||
            user.ResetPasswordExpiryUtc < DateTime.UtcNow)
        {
            return BadRequest(new { message = "Invalid or expired password reset token." });
        }

        // Hash and update the new password
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.ResetPasswordToken = null;
        user.ResetPasswordExpiryUtc = null;
        await _repository.UpdateUserAsync(user);

        // Revoke active sessions for security
        await _repository.RevokeAllSessionsByUserIdAsync(user.Id);

        return Ok(new
        {
            message = "Password has been reset successfully. You can now login with your new password."
        });
    }

    [HttpPost("verify-2fa")]
    public async Task<IActionResult> VerifyTwoFactor(
        [FromBody] VerifyTwoFactorRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.GetByEmailAsync(email);

        if (user == null || !user.IsTwoFactorEnabled)
        {
            return Unauthorized(new { message = "Invalid request or 2FA not enabled." });
        }

        if (user.TwoFactorCode != request.Code || user.TwoFactorExpiryUtc < DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Invalid or expired 2FA code." });
        }

        // Reset 2FA code after successful verification
        user.TwoFactorCode = null;
        user.TwoFactorExpiryUtc = null;
        await _repository.UpdateUserAsync(user);

        return await GenerateAuthResponseAsync(user);
    }

    [Authorize]
    [HttpPost("toggle-2fa")]
    public async Task<IActionResult> ToggleTwoFactor(
        [FromBody] ToggleTwoFactorRequest request)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var user = await _repository.GetByIdAsync(userId.Value);
        if (user == null) return NotFound();

        user.IsTwoFactorEnabled = request.Enable;
        await _repository.UpdateUserAsync(user);

        return Ok(new
        {
            message = $"Two-factor authentication has been {(request.Enable ? "enabled" : "disabled")}."
        });
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> ExternalLogin(
        [FromBody] ExternalAuthRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.GetByEmailAsync(email);

        if (user == null)
        {
            // Auto-provision user for OAuth / SSO
            user = new UserEntity
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName.Trim(),
                Email = email,
                AuthProvider = request.Provider,
                ExternalProviderId = request.ProviderKey,
                PasswordHash = "OAUTH_EXTERNAL_LOGIN",
                CreatedAt = DateTime.UtcNow
            };
            await _repository.CreateAsync(user);
        }

        return await GenerateAuthResponseAsync(user);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request)
    {
        var session = await _repository.GetSessionByRefreshTokenAsync(request.RefreshToken);

        if (session == null || !session.IsActive)
        {
            return Unauthorized(new
            {
                message = "Session is invalid or has expired."
            });
        }

        var user = await _repository.GetByIdAsync(session.UserId);
        if (user == null)
        {
            return Unauthorized(new
            {
                message = "User for this session not found."
            });
        }

        // Token rotation: Revoke current session token
        session.IsRevoked = true;
        var newRefreshToken = GenerateRefreshToken();
        session.ReplacedByToken = newRefreshToken;
        await _repository.UpdateSessionAsync(session);

        // Issue new active session
        var newSession = new UserSession
        {
            UserId = user.Id,
            RefreshToken = newRefreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        await _repository.AddSessionAsync(newSession);

        var (accessToken, expiryMinutes) = GenerateAccessToken(user);

        return Ok(new
        {
            message = "Token refreshed successfully!",
            accessToken,
            refreshToken = newRefreshToken,
            expiresInMinutes = expiryMinutes,
            expiresAtUtc = newSession.ExpiresAt
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request)
    {
        var session = await _repository.GetSessionByRefreshTokenAsync(request.RefreshToken);
        if (session != null && !session.IsRevoked)
        {
            session.IsRevoked = true;
            await _repository.UpdateSessionAsync(session);
        }

        return Ok(new
        {
            message = "Logged out successfully (session ended)."
        });
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetActiveSessions()
    {
        var userId = CurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var sessions = await _repository.GetActiveSessionsByUserIdAsync(userId.Value);

        return Ok(sessions.Select(s => new
        {
            s.Id,
            s.CreatedAt,
            s.ExpiresAt,
            s.IpAddress,
            s.UserAgent
        }));
    }

    [Authorize]
    [HttpPost("revoke-all-sessions")]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var userId = CurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _repository.RevokeAllSessionsByUserIdAsync(userId.Value);

        return Ok(new
        {
            message = "All active sessions have been revoked."
        });
    }

    private async Task<IActionResult> GenerateAuthResponseAsync(UserEntity user)
    {
        var (accessToken, expiryMinutes) = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        var session = new UserSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        await _repository.AddSessionAsync(session);

        return Ok(new
        {
            message = "Login successful!",
            accessToken,
            refreshToken,
            expiresInMinutes = expiryMinutes,
            expiresAtUtc = session.ExpiresAt
        });
    }

    private (string token, int expiryMinutes) GenerateAccessToken(UserEntity user)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key is missing.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT Issuer is missing.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT Audience is missing.");

        var expiryMinutes = int.Parse(
            _configuration["Jwt:ExpiryMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName)
        };

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiryMinutes);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
