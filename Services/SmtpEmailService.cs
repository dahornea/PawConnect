using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PawConnect.Services;

public class SmtpEmailService(IOptions<EmailSettings> options, ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly EmailSettings settings = options.Value;

    public async Task SendEmailAsync(string to, string subject, string body, List<EmailAttachment>? attachments = null, string? htmlBody = null)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogWarning("Email was not sent because the recipient address is empty. Subject: {Subject}", subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            logger.LogWarning("Email was not sent because SMTP settings are incomplete. Recipient: {Recipient}, Subject: {Subject}", to, subject);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            if ((attachments is null || attachments.Count == 0) && string.IsNullOrWhiteSpace(htmlBody))
            {
                message.Body = new TextPart("plain") { Text = body };
            }
            else
            {
                var bodyBuilder = new BodyBuilder
                {
                    TextBody = body,
                    HtmlBody = string.IsNullOrWhiteSpace(htmlBody) ? null : htmlBody
                };

                foreach (var attachment in (attachments ?? []).Where(a => a.Content.Length > 0))
                {
                    bodyBuilder.Attachments.Add(
                        attachment.FileName,
                        attachment.Content,
                        ContentType.Parse(attachment.ContentType));
                }

                message.Body = bodyBuilder.ToMessageBody();
            }

            using var client = new SmtpClient();
            var secureSocketOptions = settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, secureSocketOptions);

            if (!string.IsNullOrWhiteSpace(settings.SmtpUser) && !string.IsNullOrWhiteSpace(settings.SmtpPassword))
            {
                await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Email sent to {Recipient}. Subject: {Subject}. Attachments: {AttachmentCount}", to, subject, attachments?.Count ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Email could not be sent to {Recipient}. Subject: {Subject}", to, subject);
        }
    }
}
