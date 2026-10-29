namespace Fundoo.Notification.Domain.Entities;

public class EmailMessage
{
    public string To { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
    public bool IsHtml { get; init; }
}