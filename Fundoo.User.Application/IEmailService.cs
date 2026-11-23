namespace Fundoo.User.Application;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default);
}
