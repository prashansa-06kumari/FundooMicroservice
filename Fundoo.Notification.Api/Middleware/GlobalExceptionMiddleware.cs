using System.Net;
using System.Text.Json;

namespace Fundoo.Notification.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = exception switch
        {
            Polly.CircuitBreaker.BrokenCircuitException => (int)HttpStatusCode.ServiceUnavailable,
            Polly.Timeout.TimeoutRejectedException => (int)HttpStatusCode.GatewayTimeout,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            status = statusCode,
            message = statusCode switch
            {
                (int)HttpStatusCode.ServiceUnavailable => "Notification service is temporarily unavailable (circuit open).",
                (int)HttpStatusCode.GatewayTimeout => "The notification delivery timed out.",
                (int)HttpStatusCode.InternalServerError => "An unexpected error occurred in Notification Service.",
                _ => exception.Message
            },
            detail = _env.IsDevelopment() ? exception.StackTrace : null
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return context.Response.WriteAsync(json);
    }
}
