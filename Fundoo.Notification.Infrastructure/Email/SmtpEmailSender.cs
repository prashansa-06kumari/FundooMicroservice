using System.Net;
using System.Net.Mail;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace Fundoo.Notification.Infrastructure.Email;

public class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger,
    ResiliencePipelineProvider<string> pipelines) : IEmailSender
{
    private readonly ResiliencePipeline _pipeline = pipelines.GetPipeline("smtp");

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var o = options.Value;
        try
        {
            await _pipeline.ExecuteAsync(async token =>
            {
                // Har attempt par naya mail aur client banta hai (ek baar use ke baad dobara send nahi hote)
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
                    Credentials = new NetworkCredential(o.Username, o.Password),
                    Timeout = 10000   // socket timeout (ms), pipeline ke 15s se chhota
                };
                await client.SendMailAsync(mail, token);
            }, ct);
        }
        catch (Exception ex) when (
            ex is SmtpException or FormatException or InvalidOperationException
               or TimeoutRejectedException or BrokenCircuitException)
        {
            logger.LogError(ex, "Email send failed via SMTP host {Host}", o.Host);
            throw new EmailDeliveryException("Email could not be delivered.", ex);
        }
    }
}