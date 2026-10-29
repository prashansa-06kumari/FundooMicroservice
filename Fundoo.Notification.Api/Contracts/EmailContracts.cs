using System.ComponentModel.DataAnnotations;
namespace Fundoo.Notification.Api.Contracts;

public class SendEmailRequest
{
    [Required, EmailAddress, MaxLength(256)] public string To { get; set; } = "";
    [Required, MaxLength(200)] public string Subject { get; set; } = "";
    [Required, MaxLength(50000)] public string Body { get; set; } = "";
    public bool IsHtml { get; set; }
}