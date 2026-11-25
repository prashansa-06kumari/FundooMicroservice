using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fundoo.User.Api.Models;
using Fundoo.User.Api.Services;
using Fundoo.User.Application;
using Fundoo.User.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[Tags("Auth")]
[Route("api/v1/auth")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher<UserEntity> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IEmailService _emailService;
    private readonly IHttpClientFactory _httpClientFactory;

    public UsersController(
        IUserRepository repository,
        IPasswordHasher<UserEntity> passwordHasher,
        IConfiguration configuration,
        IKafkaProducer kafkaProducer,
        IEmailService emailService,
        IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _kafkaProducer = kafkaProducer;
        _emailService = emailService;
        _httpClientFactory = httpClientFactory;
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? User.FindFirstValue("userId");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    private string? CurrentUserEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email)
               ?? User.FindFirstValue("email");
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

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var fullName = $"{firstName} {lastName}".Trim();

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            FullName = fullName,
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
            firstName = user.FirstName,
            lastName = user.LastName,
            fullName = user.FullName,
            timestamp = DateTime.UtcNow
        });
        await _kafkaProducer.ProduceAsync("user-events", user.Id.ToString(), eventPayload);

        return Ok(new
        {
            message = "User registered successfully!",
            user.Id,
            user.FirstName,
            user.LastName,
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

        // Check if 2FA is enabled (Google Authenticator)
        if (user.IsTwoFactorEnabled)
        {
            return Ok(new
            {
                requires2fa = true,
                requiresTwoFactor = true,
                email = user.Email,
                message = "Google Authenticator 2FA is required. Open your Google Authenticator app and enter the 6-digit code in /api/v1/auth/login/2fa."
            });
        }

        return await GenerateAuthResponseAsync(user);
    }

    [HttpPost("login/2fa")]
    public async Task<IActionResult> TwoFactorLogin(
        [FromBody] TwoFactorLoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.GetByEmailAsync(email);

        if (user == null || !user.IsTwoFactorEnabled)
        {
            return Unauthorized(new { message = "Invalid request or 2FA is not enabled for this user." });
        }

        var secret = user.TwoFactorSecret ?? user.TwoFactorCode;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Unauthorized(new { message = "Two-factor secret key is missing. Please set up 2FA again via /api/v1/auth/2fa/setup." });
        }

        // Validate 6-digit code from Google Authenticator
        bool isValid = TotpService.ValidateCode(secret, request.Code);
        if (!isValid)
        {
            return Unauthorized(new { message = "Invalid 6-digit Google Authenticator code. Please check your app and try again." });
        }

        return await GenerateAuthResponseAsync(user);
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(
        [FromBody] GoogleLoginRequest request)
    {
        string email;
        string fullName;
        string? googleId = request.GoogleId;

        // If IdToken is provided, verify it directly with Google Cloud OAuth2 tokeninfo endpoint
        if (!string.IsNullOrWhiteSpace(request.IdToken))
        {
            var httpClient = _httpClientFactory.CreateClient();
            var googleVerifyUrl = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(request.IdToken.Trim())}";

            var response = await httpClient.GetAsync(googleVerifyUrl);
            if (!response.IsSuccessStatusCode)
            {
                return Unauthorized(new
                {
                    message = "Google ID Token verification failed. The token is invalid or expired."
                });
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("email", out var emailProp) || string.IsNullOrWhiteSpace(emailProp.GetString()))
            {
                return Unauthorized(new { message = "Invalid Google token: email not found." });
            }

            email = emailProp.GetString()!.Trim().ToLowerInvariant();

            // Verify Google Client ID if configured in appsettings.json
            var configuredClientId = _configuration["Google:ClientId"];
            if (!string.IsNullOrWhiteSpace(configuredClientId) &&
                !configuredClientId.Contains("YOUR_GOOGLE_CLIENT_ID", StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("aud", out var audProp) &&
                    !string.Equals(audProp.GetString(), configuredClientId, StringComparison.Ordinal))
                {
                    return Unauthorized(new { message = "Google Token audience does not match configured Google Client ID." });
                }
            }

            fullName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? email.Split('@')[0] : email.Split('@')[0];
            googleId = root.TryGetProperty("sub", out var subProp) ? subProp.GetString() : googleId;
        }
        else if (!string.IsNullOrWhiteSpace(request.Email))
        {
            // Direct profile payload (convenient for local Swagger testing)
            email = request.Email.Trim().ToLowerInvariant();
            fullName = string.IsNullOrWhiteSpace(request.FullName) ? email.Split('@')[0] : request.FullName.Trim();
        }
        else
        {
            return BadRequest(new
            {
                message = "Either 'idToken' (Google OAuth 2.0 Token) or 'email' must be provided."
            });
        }

        var user = await _repository.GetByEmailAsync(email);

        if (user == null)
        {
            var nameParts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.Length > 0 ? nameParts[0] : fullName;
            var lastName = nameParts.Length > 1 ? nameParts[1] : "";

            // Auto-provision user for Google SSO
            user = new UserEntity
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                FullName = fullName,
                Email = email,
                AuthProvider = "Google",
                ExternalProviderId = googleId,
                PasswordHash = "OAUTH_GOOGLE_ACCOUNT",
                CreatedAt = DateTime.UtcNow
            };
            await _repository.CreateAsync(user);
        }

        return await GenerateAuthResponseAsync(user);
    }

    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor(
        [FromBody] TwoFactorSetupRequest? request)
    {
        var email = request?.Email?.Trim().ToLowerInvariant() ?? CurrentUserEmail();
        var userId = CurrentUserId();

        UserEntity? user = null;
        if (userId != null)
        {
            user = await _repository.GetByIdAsync(userId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            user = await _repository.GetByEmailAsync(email);
        }

        if (user == null)
        {
            return BadRequest(new { message = "User not found. Provide email or authenticate with Bearer token." });
        }

        // Generate RFC 6238 Base32 secret key for Google Authenticator
        var secret = TotpService.GenerateSecretKey(10);
        user.TwoFactorSecret = secret;
        user.TwoFactorCode = secret;
        await _repository.UpdateUserAsync(user);

        var otpAuthUri = TotpService.GenerateOtpAuthUri(user.Email, secret, "FundooNotes");
        var qrCodeUrl = TotpService.GenerateQrCodeUrl(otpAuthUri);

        return Ok(new
        {
            manualEntryKey = secret,
            qrCodeUrl,
            otpauthUri = otpAuthUri,
            message = "Open Google Authenticator on your phone. Tap '+' -> 'Scan a QR code' (open qrCodeUrl) OR 'Enter a setup key' (type manualEntryKey). Then verify using /api/v1/auth/2fa/enable with the 6-digit code showing in the app."
        });
    }

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor(
        [FromBody] TwoFactorEnableRequest request)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? CurrentUserEmail();
        var userId = CurrentUserId();

        UserEntity? user = null;
        if (userId != null)
        {
            user = await _repository.GetByIdAsync(userId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            user = await _repository.GetByEmailAsync(email);
        }

        if (user == null)
        {
            return BadRequest(new { message = "User not found." });
        }

        var secret = user.TwoFactorSecret ?? user.TwoFactorCode;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return BadRequest(new { message = "2FA setup has not been initiated. Call /api/v1/auth/2fa/setup first." });
        }

        // Validate code from Google Authenticator
        bool isValid = TotpService.ValidateCode(secret, request.Code);
        if (!isValid)
        {
            return BadRequest(new { message = "Invalid 6-digit Google Authenticator code. Make sure your phone's clock is synchronized." });
        }

        user.IsTwoFactorEnabled = true;
        await _repository.UpdateUserAsync(user);

        return Ok(new
        {
            message = "Two-factor authentication with Google Authenticator has been enabled successfully!"
        });
    }

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(
        [FromBody] TwoFactorDisableRequest? request)
    {
        var email = request?.Email?.Trim().ToLowerInvariant() ?? CurrentUserEmail();
        var userId = CurrentUserId();

        UserEntity? user = null;
        if (userId != null)
        {
            user = await _repository.GetByIdAsync(userId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            user = await _repository.GetByEmailAsync(email);
        }

        if (user == null)
        {
            return BadRequest(new { message = "User not found." });
        }

        user.IsTwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorCode = null;
        await _repository.UpdateUserAsync(user);

        return Ok(new
        {
            message = "Two-factor authentication has been disabled."
        });
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

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest? request)
    {
        if (request != null && !string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var session = await _repository.GetSessionByRefreshTokenAsync(request.RefreshToken);
            if (session != null && !session.IsRevoked)
            {
                session.IsRevoked = true;
                await _repository.UpdateSessionAsync(session);
            }
        }
        else
        {
            var userId = CurrentUserId();
            if (userId != null)
            {
                await _repository.RevokeAllSessionsByUserIdAsync(userId.Value);
            }
        }

        return Ok(new
        {
            message = "Logged out successfully (session ended)."
        });
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
