using Fundoo.Notification.Api.Middleware;
using Fundoo.Notification.Api.Security;
using Fundoo.Notification.Api.Services;
using Fundoo.Notification.Infrastructure;
using Microsoft.OpenApi.Models;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog
builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddNotificationInfrastructure(builder.Configuration);

// Add HttpClient
builder.Services.AddHttpClient();

// Add Kafka Consumer BackgroundService
builder.Services.AddHostedService<KafkaNotificationConsumer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("InternalKey", new OpenApiSecurityScheme
    {
        Name = InternalApiKeyAttribute.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "InternalKey" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));
app.Run();
