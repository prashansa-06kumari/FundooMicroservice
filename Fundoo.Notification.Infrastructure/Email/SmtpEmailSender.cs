using System.Net;
using System.Net.Mail;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Fundoo.Notification.Infrastructure.Email;

public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var o = options.Value;
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(o.FromAddress, o.FromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = message.IsHtml
            };
            mail.To.Add(message.To);

            using var client = new SmtpClient(o.Host, o.Port)
            {
                EnableSsl = o.EnableSsl,
                Credentials = new NetworkCredential(o.Username, o.Password)
            };
            await client.SendMailAsync(mail, ct);
        }
        catch (Exception ex) when (ex is SmtpException or FormatException or InvalidOperationException)
        {
            logger.LogError(ex, "Email send failed via SMTP host {Host}", o.Host);
            throw new EmailDeliveryException("Email could not be delivered.", ex);
        }
    }
}