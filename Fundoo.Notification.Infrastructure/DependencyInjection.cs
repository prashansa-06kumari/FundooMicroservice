using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.Emails;
using Fundoo.Notification.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Fundoo.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(config.GetSection("Smtp"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host) && !string.IsNullOrWhiteSpace(o.FromAddress),
                      "Smtp:Host and Smtp:FromAddress are required.")
            .ValidateOnStart();

        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}