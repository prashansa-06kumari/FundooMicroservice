using System.Net.Mail;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Domain.Entities;
namespace Fundoo.Notification.Application.Emails;

public enum SendEmailResult { Sent, InvalidRecipient, InvalidContent, DeliveryFailed }

public class NotificationService(IEmailSender sender) : INotificationService
{
    public async Task<SendEmailResult> SendEmailAsync(string to, string subject, string body, bool isHtml, CancellationToken ct = default)
    {
        to = to.Trim();
        if (!MailAddress.TryCreate(to, out var address) || address.Address != to)
            return SendEmailResult.InvalidRecipient;

        subject = subject.Trim();
        if (subject.Length == 0 || subject.Contains('\r') || subject.Contains('\n') || string.IsNullOrWhiteSpace(body))
            return SendEmailResult.InvalidContent;

        try
        {
            await sender.SendAsync(new EmailMessage { To = to, Subject = subject, Body = body, IsHtml = isHtml }, ct);
            return SendEmailResult.Sent;
        }
        catch (EmailDeliveryException) { return SendEmailResult.DeliveryFailed; }
    }
}