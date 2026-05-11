using System.Diagnostics;

namespace PawConnect.Services;

public class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body, List<EmailAttachment>? attachments = null, string? htmlBody = null)
    {
        var message = $"Mock email to {to}: {subject} - {body} Attachments: {attachments?.Count ?? 0} Html: {!string.IsNullOrWhiteSpace(htmlBody)}";
        logger.LogInformation("{Message}", message);
        Debug.WriteLine(message);
        return Task.CompletedTask;
    }
}
