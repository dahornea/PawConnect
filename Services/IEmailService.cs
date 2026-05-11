namespace PawConnect.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, List<EmailAttachment>? attachments = null, string? htmlBody = null);
}
