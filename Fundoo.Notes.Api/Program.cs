using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Fundoo.Notes.Api.Middleware;
using Fundoo.Notes.Api.Validators;
using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Infrastructure.Data;
using Fundoo.Notes.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Controllers and FluentValidation
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateNoteRequestValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Fundoo Notes API", Version = "v1" });

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

// HttpClient
builder.Services.AddHttpClient();

// Database connection
var connectionString =
    builder.Configuration.GetConnectionString("NotesDb")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:NotesDb is missing from configuration.");

builder.Services.AddSingleton(
    new NotesConnectionFactory(connectionString));

// Repositories
builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<ILabelRepository, LabelRepository>();
builder.Services.AddScoped<ICollaboratorRepository, CollaboratorRepository>();

// JWT configuration
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing.");

var issuer = builder.Configuration["Jwt:Issuer"] ?? "FundooUserApi";
var audience = builder.Configuration["Jwt:Audience"] ?? "FundooClient";

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must contain at least 32 bytes.");
}

// JWT Authentication
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

                    while (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = token.Substring("Bearer ".Length).Trim();
                    }

                    token = token.Trim('"', '\'', ' ');

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
                ValidIssuers = new[] { issuer, "FundooUserApi" },

                // Disable strict audience matching across microservices to accept valid Fundoo tokens
                ValidateAudience = false,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
    });

// Authorization
builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-migrate Notes, Labels, NoteLabels, and Collaborators database schema
try
{
    var connFactory = app.Services.GetRequiredService<NotesConnectionFactory>();
    await using var conn = connFactory.CreateConnection();
    await conn.OpenAsync();

    const string migrationSql = """
        CREATE TABLE IF NOT EXISTS "Notes" (
            "Id" UUID PRIMARY KEY,
            "UserId" UUID NOT NULL,
            "Title" VARCHAR(200) NOT NULL,
            "Content" TEXT NOT NULL,
            "Color" VARCHAR(30) NULL,
            "IsPinned" BOOLEAN NOT NULL DEFAULT FALSE,
            "IsArchived" BOOLEAN NOT NULL DEFAULT FALSE,
            "IsTrashed" BOOLEAN NOT NULL DEFAULT FALSE,
            "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            "UpdatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        CREATE INDEX IF NOT EXISTS "IX_Notes_UserId" ON "Notes" ("UserId");

        CREATE TABLE IF NOT EXISTS "Labels" (
            "Id" UUID PRIMARY KEY,
            "UserId" UUID NOT NULL,
            "Name" VARCHAR(100) NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Labels_UserId_Name" ON "Labels" ("UserId", "Name");

        CREATE TABLE IF NOT EXISTS "NoteLabels" (
            "NoteId" UUID NOT NULL,
            "LabelId" UUID NOT NULL,
            CONSTRAINT "PK_NoteLabels" PRIMARY KEY ("NoteId", "LabelId")
        );

        CREATE TABLE IF NOT EXISTS "Collaborators" (
            "Id" UUID PRIMARY KEY,
            "NoteId" UUID NOT NULL REFERENCES "Notes"("Id") ON DELETE CASCADE,
            "CollaboratorEmail" VARCHAR(255) NOT NULL,
            "CreatedAtUtc" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            CONSTRAINT "UQ_Collaborators_NoteId_Email" UNIQUE ("NoteId", "CollaboratorEmail")
        );
        CREATE INDEX IF NOT EXISTS "IX_Collaborators_NoteId" ON "Collaborators" ("NoteId");
        CREATE INDEX IF NOT EXISTS "IX_Collaborators_Email" ON "Collaborators" ("CollaboratorEmail");
        """;

    await using var cmd = new Npgsql.NpgsqlCommand(migrationSql, conn);
    await cmd.ExecuteNonQueryAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Could not automatically apply Notes database schema: {Message}", ex.Message);
}

// Global Exception Handling Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger UI configuration (available in all environments)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "Fundoo Notes API v1");

    options.RoutePrefix = "swagger";
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    service = "Fundoo.Notes.Api",
    status = "Healthy"
}));

// Middleware
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

app.Run();
