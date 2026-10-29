using Fundoo.Notification.Domain.Entities;
namespace Fundoo.Notification.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
public class EmailDeliveryException(string message, Exception? inner = null) : Exception(message, inner);