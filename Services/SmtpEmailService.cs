using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class SmtpEmailService(
    IOptions<EmailSettings> options,
    ILogger<SmtpEmailService> logger,
    IDbContextFactory<ApplicationDbContext>? contextFactory = null,
    INotificationPreferenceService? preferenceService = null,
    INotificationDeliveryLogService? deliveryLogService = null) : IEmailService
{
    private readonly EmailSettings settings = options.Value;

    public async Task SendEmailAsync(string to, string subject, string body, List<EmailAttachment>? attachments = null, string? htmlBody = null)
    {
        var deliveryContext = await ResolveDeliveryContextAsync(to, subject);

        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogWarning("Email was not sent because the recipient address is empty. Subject: {Subject}", subject);
            await TryLogEmailDeliveryAsync(deliveryContext, NotificationDeliveryStatus.Skipped, "Recipient address is empty.");
            return;
        }

        if (!settings.Enabled)
        {
            logger.LogInformation("Email skipped because email delivery is disabled by configuration. Recipient: {Recipient}, Subject: {Subject}", to, subject);
            await TryLogEmailDeliveryAsync(deliveryContext, NotificationDeliveryStatus.Skipped, "Email delivery is disabled by configuration.");
            return;
        }

        if (!deliveryContext.IsAccountSecurityEmail &&
            !string.IsNullOrWhiteSpace(deliveryContext.UserId) &&
            preferenceService is not null &&
            !await preferenceService.IsChannelEnabledAsync(deliveryContext.UserId, deliveryContext.NotificationType, NotificationChannel.Email))
        {
            logger.LogInformation("Email skipped by notification preference. Recipient: {Recipient}, Subject: {Subject}", to, subject);
            await TryLogEmailDeliveryAsync(deliveryContext, NotificationDeliveryStatus.DisabledByPreference);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            logger.LogWarning("Email was not sent because SMTP settings are incomplete. Recipient: {Recipient}, Subject: {Subject}", to, subject);
            await TryLogEmailDeliveryAsync(deliveryContext, NotificationDeliveryStatus.Failed, "SMTP settings are incomplete.");
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            message.Body = EmailMimeBuilder.BuildBody(body, htmlBody, attachments);

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
            await TryLogEmailDeliveryAsync(deliveryContext, NotificationDeliveryStatus.Sent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Email could not be sent to {Recipient}. Subject: {Subject}", to, subject);
            await TryLogEmailDeliveryAsync(deliveryContext, NotificationDeliveryStatus.Failed, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task<EmailDeliveryContext> ResolveDeliveryContextAsync(string to, string subject)
    {
        var normalizedRecipient = string.IsNullOrWhiteSpace(to) ? null : to.Trim();
        var notificationType = NotificationEventTypeMapper.FromEmailSubject(subject);
        var isAccountSecurityEmail = NotificationEventTypeMapper.IsAccountSecurityEmail(subject);
        string? userId = null;

        if (contextFactory is not null && !string.IsNullOrWhiteSpace(normalizedRecipient))
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();
                var normalizedEmail = normalizedRecipient.ToUpperInvariant();
                userId = await context.Users
                    .AsNoTracking()
                    .Where(user => user.NormalizedEmail == normalizedEmail)
                    .Select(user => user.Id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Email recipient could not be matched to a PawConnect user.");
            }
        }

        return new EmailDeliveryContext(normalizedRecipient, userId, notificationType, isAccountSecurityEmail, subject);
    }

    private async Task TryLogEmailDeliveryAsync(
        EmailDeliveryContext deliveryContext,
        NotificationDeliveryStatus status,
        string? errorMessage = null)
    {
        if (deliveryLogService is null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            await deliveryLogService.LogDeliveryAsync(new NotificationDeliveryLogCreateRequest(
                deliveryContext.NotificationType,
                NotificationChannel.Email,
                status,
                UserId: deliveryContext.UserId,
                Recipient: deliveryContext.Recipient,
                Subject: deliveryContext.Subject,
                ErrorMessage: errorMessage,
                SentAt: status == NotificationDeliveryStatus.Sent ? now : null,
                FailedAt: status == NotificationDeliveryStatus.Failed ? now : null));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Email delivery log creation failed. Recipient: {Recipient}", deliveryContext.Recipient);
        }
    }

    private sealed record EmailDeliveryContext(
        string? Recipient,
        string? UserId,
        NotificationEventType NotificationType,
        bool IsAccountSecurityEmail,
        string Subject);
}
