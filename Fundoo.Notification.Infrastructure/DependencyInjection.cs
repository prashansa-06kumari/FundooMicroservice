using System.Net.Mail;
using System.Net.Sockets;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.Emails;
using Fundoo.Notification.Infrastructure.Data;
using Fundoo.Notification.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Fundoo.Notification.Infrastructure;

public static class DependencyInjection
{
    private static bool IsTransientSmtp(Exception ex) =>
        ex is SocketException or IOException or TimeoutException
        || (ex is SmtpException e &&
            (e.StatusCode == SmtpStatusCode.GeneralFailure ||
             e.StatusCode == SmtpStatusCode.ServiceNotAvailable ||
             e.StatusCode == SmtpStatusCode.MailboxBusy ||
             e.StatusCode == SmtpStatusCode.InsufficientStorage));

    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("NotificationDb") 
            ?? config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=FundooNotificationDb;Username=postgres;Password=admin123";

        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddOptions<SmtpOptions>()
            .Bind(config.GetSection("Smtp"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host) && !string.IsNullOrWhiteSpace(o.FromAddress),
                      "Smtp:Host and Smtp:FromAddress are required.")
            .ValidateOnStart();

        // SMTP pipeline (Polly v8): Bulkhead -> Retry(+jitter) -> Circuit breaker -> Timeout
        services.AddResiliencePipeline("smtp", builder =>
        {
            builder.AddConcurrencyLimiter(5, 20);

            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => new ValueTask<bool>(
                    args.Outcome.Exception is { } ex && IsTransientSmtp(ex))
            });

            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.7,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = args => new ValueTask<bool>(
                    args.Outcome.Exception is { } ex && IsTransientSmtp(ex))
            });

            builder.AddTimeout(TimeSpan.FromSeconds(15));
        });

        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
