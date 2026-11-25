using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Fundoo.User.Api.Middleware;
using Fundoo.User.Api.Validators;
using Fundoo.User.Application;
using Fundoo.User.Domain;
using Fundoo.User.Infrastructure;
using Fundoo.User.Infrastructure.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Add Controllers and FluentValidation
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Fundoo User API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme. Enter your token directly.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add HttpClient
builder.Services.AddHttpClient();

// Database connection
var userConnectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing.");

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(userConnectionString));

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<
    IPasswordHasher<UserEntity>,
    PasswordHasher<UserEntity>>();

// Kafka Producer
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

// Direct Email Service
builder.Services.AddScoped<IEmailService, Fundoo.User.Infrastructure.Services.EmailService>();

// JWT Configuration
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing.");

var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is missing.");

var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is missing.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must contain at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    var token = authHeader.Trim();

                    // Remove any repeated "Bearer " prefixes
                    while (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = token.Substring("Bearer ".Length).Trim();
                    }

                    // Clean quotes if copied from JSON
                    token = token.Trim('"', '\'', ' ');

                    // If user accidentally pasted `accessToken", "refreshToken": "..."`
                    if (token.Contains("\","))
                    {
                        token = token.Substring(0, token.IndexOf("\",")).Trim('"', ' ');
                    }

                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-migrate missing columns and tables in PostgreSQL database
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    await db.Database.ExecuteSqlRawAsync(@"
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""FirstName"" text NULL;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""LastName"" text NULL;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""FullName"" text NULL;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""AuthProvider"" text NOT NULL DEFAULT 'Local';
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ExternalProviderId"" text NULL;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""IsTwoFactorEnabled"" boolean NOT NULL DEFAULT FALSE;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""TwoFactorSecret"" text NULL;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""TwoFactorCode"" text NULL;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""TwoFactorExpiryUtc"" timestamp with time zone NULL;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ResetPasswordToken"" text NULL;
        ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ResetPasswordExpiryUtc"" timestamp with time zone NULL;

        CREATE TABLE IF NOT EXISTS ""UserSessions"" (
            ""Id"" uuid NOT NULL,
            ""UserId"" uuid NOT NULL,
            ""RefreshToken"" character varying(500) NOT NULL,
            ""CreatedAt"" timestamp with time zone NOT NULL,
            ""ExpiresAt"" timestamp with time zone NOT NULL,
            ""IsRevoked"" boolean NOT NULL,
            ""ReplacedByToken"" text NULL,
            ""IpAddress"" character varying(45) NULL,
            ""UserAgent"" character varying(500) NULL,
            CONSTRAINT ""PK_UserSessions"" PRIMARY KEY (""Id""),
            CONSTRAINT ""FK_UserSessions_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_UserSessions_RefreshToken"" ON ""UserSessions"" (""RefreshToken"");
        CREATE INDEX IF NOT EXISTS ""IX_UserSessions_UserId"" ON ""UserSessions"" (""UserId"");
    ");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Could not automatically apply schema migrations: {Message}", ex.Message);
}

// Global Exception Handling Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Enable Swagger in all environments (Local and Deployed)
app.UseSwagger();
app.UseSwaggerUI();

// Route rewrite: Forward /api/auth and /api/users to /api/v1/auth seamlessly
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
    {
        context.Request.Path = "/api/v1/auth" + path.Substring("/api/auth".Length);
    }
    else if (path.StartsWith("/api/users", StringComparison.OrdinalIgnoreCase))
    {
        context.Request.Path = "/api/v1/auth" + path.Substring("/api/users".Length);
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    service = "Fundoo.User.Api",
    status = "Healthy"
}));

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
