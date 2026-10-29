using Fundoo.Notification.Application.Emails;
namespace Fundoo.Notification.Application.Abstractions;

public interface INotificationService
{
    Task<SendEmailResult> SendEmailAsync(string to, string subject, string body, bool isHtml, CancellationToken ct = default);
}