using PawConnect.Services;

namespace PawConnect.IntegrationTests.Infrastructure;

public sealed class IntegrationTestEmailService : IEmailService
{
    public bool ThrowOnSend { get; set; }

    public List<(string To, string Subject, string Body)> SentEmails { get; } = [];

    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        List<EmailAttachment>? attachments = null,
        string? htmlBody = null)
    {
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("Integration email failure.");
        }

        SentEmails.Add((to, subject, body));
        return Task.CompletedTask;
    }
}
