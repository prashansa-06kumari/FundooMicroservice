using System.Net.Http.Json;
using Fundoo.User.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fundoo.User.Infrastructure.Notifications;

public class NotificationApiEmailNotifier(HttpClient http, ILogger<NotificationApiEmailNotifier> logger) : IEmailNotifier
{
    public async Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token, CancellationToken ct = default)
    {
        var body = $"Hi {firstName},\n\n" +
                   "Use the token below to reset your Fundoo password. It is valid for 15 minutes.\n\n" +
                   $"{token}\n\n" +
                   "If you did not request this, you can ignore this email.";

        try
        {
            using var response = await http.PostAsJsonAsync("api/notifications/email",
                new { to = toEmail, subject = "Fundoo password reset", body, isHtml = false }, ct);

            if (response.IsSuccessStatusCode)
                logger.LogInformation("Password reset email request accepted by Notification service");
            else
                logger.LogWarning("Notification service returned {StatusCode} for a password reset email", (int)response.StatusCode);
        }
        catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !ct.IsCancellationRequested)
        {
            // Token/email log nahi karte. Notification service down ho to bhi API ka jawab same rehta hai.
            logger.LogWarning(ex, "Notification service could not be reached");
        }
    }
}