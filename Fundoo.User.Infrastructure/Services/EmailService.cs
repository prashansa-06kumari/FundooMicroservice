using System.Net;
using System.Net.Mail;
using Fundoo.User.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fundoo.User.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
    {
        var host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.Parse(_configuration["Smtp:Port"] ?? "587");
        var enableSsl = bool.Parse(_configuration["Smtp:EnableSsl"] ?? "true");
        var username = _configuration["Smtp:Username"] ?? "";
        var password = _configuration["Smtp:Password"] ?? "";
        var fromAddress = _configuration["Smtp:FromAddress"] ?? username;
        var fromName = _configuration["Smtp:FromName"] ?? "Fundoo Notes";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("SMTP credentials not configured in appsettings.json. Email to {To} could not be sent.", to);
            return;
        }

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };
            mail.To.Add(to);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password),
                Timeout = 10000
            };

            await client.SendMailAsync(mail, ct);
            _logger.LogInformation("Email successfully sent to {To} with subject: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Message}", to, ex.Message);
        }
    }
}
