using Fundoo.Notification.Infrastructure;
using Fundoo.Notification.Api.Security;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNotificationInfrastructure(builder.Configuration);
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

// Polly ke timeout / open-circuit errors par 500 ki jagah saaf 503/504
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Polly.CircuitBreaker.BrokenCircuitException)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsync("Service temporarily unavailable.");
    }
    catch (Polly.Timeout.TimeoutRejectedException)
    {
        context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        await context.Response.WriteAsync("The operation timed out.");
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));
app.Run();