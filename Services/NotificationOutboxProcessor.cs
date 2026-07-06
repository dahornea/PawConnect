using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class NotificationOutboxProcessor(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEmailService emailService,
    ILogger<NotificationOutboxProcessor> logger,
    INotificationPreferenceService? preferenceService = null,
    INotificationDeliveryLogService? deliveryLogService = null) : INotificationOutboxProcessor
{
    public async Task<NotificationOutboxProcessResult> ProcessDueAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var safeBatchSize = Math.Clamp(batchSize, 1, 100);
        var dueMessages = await LoadDueMessageIdsAsync(safeBatchSize, cancellationToken);
        var sent = 0;
        var failed = 0;
        var deadLettered = 0;
        var skipped = 0;

        foreach (var messageId in dueMessages)
        {
            var result = await ProcessOneAsync(messageId, cancellationToken);
            switch (result)
            {
                case ProcessOneResult.Sent:
                    sent++;
                    break;
                case ProcessOneResult.Failed:
                    failed++;
                    break;
                case ProcessOneResult.DeadLettered:
                    deadLettered++;
                    break;
                case ProcessOneResult.Skipped:
                    skipped++;
                    break;
            }
        }

        return new NotificationOutboxProcessResult(dueMessages.Count, sent, failed, deadLettered, skipped);
    }

    private async Task<List<int>> LoadDueMessageIdsAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        return await context.NotificationOutboxMessages
            .AsNoTracking()
            .Where(message =>
                (message.Status == NotificationOutboxStatus.Pending || message.Status == NotificationOutboxStatus.Failed) &&
                (message.NextAttemptAt == null || message.NextAttemptAt <= now) &&
                message.AttemptCount < message.MaxAttempts)
            .OrderBy(message => message.NextAttemptAt ?? message.CreatedAt)
            .ThenBy(message => message.Id)
            .Take(batchSize)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<ProcessOneResult> ProcessOneAsync(int messageId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var message = await context.NotificationOutboxMessages
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);

        if (message is null ||
            message.Status is NotificationOutboxStatus.Processing or NotificationOutboxStatus.Sent or NotificationOutboxStatus.Cancelled or NotificationOutboxStatus.DeadLetter)
        {
            return ProcessOneResult.Skipped;
        }

        if (message.NextAttemptAt is not null && message.NextAttemptAt > now)
        {
            return ProcessOneResult.Skipped;
        }

        message.Status = NotificationOutboxStatus.Processing;
        message.LastAttemptAt = now;
        message.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var deliveryOutcome = await DeliverAsync(context, message, cancellationToken);
            message.Status = deliveryOutcome == DeliveryOutcome.Cancelled
                ? NotificationOutboxStatus.Cancelled
                : NotificationOutboxStatus.Sent;
            message.SentAt = deliveryOutcome == DeliveryOutcome.Sent ? DateTime.UtcNow : null;
            message.LastError = null;
            message.NextAttemptAt = null;
            message.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return deliveryOutcome == DeliveryOutcome.Sent ? ProcessOneResult.Sent : ProcessOneResult.Skipped;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification outbox message {OutboxMessageId} failed.", message.Id);

            message.AttemptCount++;
            message.LastError = NormalizeError($"{ex.GetType().Name}: {ex.Message}");
            message.UpdatedAt = DateTime.UtcNow;

            if (message.AttemptCount >= message.MaxAttempts)
            {
                message.Status = NotificationOutboxStatus.DeadLetter;
                message.NextAttemptAt = null;
                await context.SaveChangesAsync(cancellationToken);
                await LogDeliveryAsync(message, NotificationDeliveryStatus.Failed, message.LastError, cancellationToken);
                return ProcessOneResult.DeadLettered;
            }

            message.Status = NotificationOutboxStatus.Failed;
            message.NextAttemptAt = message.UpdatedAt.Add(GetRetryDelay(message.AttemptCount));
            await context.SaveChangesAsync(cancellationToken);
            await LogDeliveryAsync(message, NotificationDeliveryStatus.Failed, message.LastError, cancellationToken);
            return ProcessOneResult.Failed;
        }
    }

    private async Task<DeliveryOutcome> DeliverAsync(
        ApplicationDbContext context,
        NotificationOutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Channel == NotificationChannel.Email)
        {
            if (string.IsNullOrWhiteSpace(message.RecipientEmail))
            {
                throw new InvalidOperationException("Email outbox message does not have a recipient email.");
            }

            if (!string.IsNullOrWhiteSpace(message.RecipientUserId) &&
                preferenceService is not null &&
                !await preferenceService.IsChannelEnabledAsync(
                    message.RecipientUserId,
                    message.NotificationType,
                    NotificationChannel.Email,
                    cancellationToken))
            {
                await LogDeliveryAsync(message, NotificationDeliveryStatus.DisabledByPreference, null, cancellationToken);
                return DeliveryOutcome.Cancelled;
            }

            var attemptStartedAt = DateTime.UtcNow;
            await emailService.SendEmailAsync(message.RecipientEmail, message.Subject, message.Body);
            var deliveryStatus = await TryResolveEmailDeliveryStatusAsync(context, message, attemptStartedAt, cancellationToken);

            if (deliveryStatus?.Status == NotificationDeliveryStatus.Failed)
            {
                throw new InvalidOperationException(deliveryStatus.ErrorMessage ?? "Email delivery failed.");
            }

            if (deliveryStatus?.Status is NotificationDeliveryStatus.Skipped or NotificationDeliveryStatus.DisabledByPreference)
            {
                return DeliveryOutcome.Cancelled;
            }

            return DeliveryOutcome.Sent;
        }

        if (string.IsNullOrWhiteSpace(message.RecipientUserId))
        {
            throw new InvalidOperationException("In-app outbox message does not have a recipient user.");
        }

        if (preferenceService is not null &&
            !await preferenceService.IsChannelEnabledAsync(
                message.RecipientUserId,
                message.NotificationType,
                NotificationChannel.InApp,
                cancellationToken))
        {
            await LogDeliveryAsync(message, NotificationDeliveryStatus.DisabledByPreference, null, cancellationToken);
            return DeliveryOutcome.Cancelled;
        }

        var notification = new Notification
        {
            UserId = message.RecipientUserId,
            Title = message.Subject,
            Message = message.Body.Length <= 500 ? message.Body : message.Body[..500],
            Category = MapCategory(message.NotificationType),
            Type = NotificationType.Info,
            Link = message.Link,
            RelatedEntityName = message.RelatedEntityType,
            RelatedEntityId = message.RelatedEntityId,
            CreatedAt = DateTime.UtcNow
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync(cancellationToken);
        await LogDeliveryAsync(message, NotificationDeliveryStatus.Sent, null, cancellationToken, notification.Id);
        return DeliveryOutcome.Sent;
    }

    private static async Task<NotificationDeliveryLog?> TryResolveEmailDeliveryStatusAsync(
        ApplicationDbContext context,
        NotificationOutboxMessage message,
        DateTime attemptStartedAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.RecipientEmail))
        {
            return null;
        }

        return await context.NotificationDeliveryLogs
            .AsNoTracking()
            .Where(log =>
                log.Channel == NotificationChannel.Email &&
                log.Recipient == message.RecipientEmail &&
                log.Subject == message.Subject &&
                log.CreatedAt >= attemptStartedAt.AddSeconds(-2))
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task LogDeliveryAsync(
        NotificationOutboxMessage message,
        NotificationDeliveryStatus status,
        string? errorMessage,
        CancellationToken cancellationToken,
        int? notificationId = null)
    {
        if (deliveryLogService is null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            await deliveryLogService.LogDeliveryAsync(new NotificationDeliveryLogCreateRequest(
                message.NotificationType,
                message.Channel,
                status,
                NotificationId: notificationId,
                UserId: message.RecipientUserId,
                Recipient: message.RecipientEmail,
                Subject: message.Subject,
                ErrorMessage: errorMessage,
                SentAt: status == NotificationDeliveryStatus.Sent ? now : null,
                FailedAt: status == NotificationDeliveryStatus.Failed ? now : null,
                RetryCount: message.AttemptCount,
                RelatedEntityType: message.RelatedEntityType,
                RelatedEntityId: message.RelatedEntityId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification outbox delivery log creation failed for outbox message {OutboxMessageId}.", message.Id);
        }
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        return attemptCount switch
        {
            <= 1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromHours(1)
        };
    }

    private static NotificationCategory MapCategory(NotificationEventType notificationType)
    {
        return notificationType switch
        {
            NotificationEventType.AdoptionRequestUpdates or NotificationEventType.VisitReminders => NotificationCategory.Adoption,
            NotificationEventType.Messages => NotificationCategory.Adoption,
            NotificationEventType.ResourceAlerts => NotificationCategory.Resource,
            NotificationEventType.ReportUpdates => NotificationCategory.Report,
            NotificationEventType.ShelterApplicationUpdates => NotificationCategory.ShelterApplication,
            NotificationEventType.LostFoundUpdates => NotificationCategory.System,
            NotificationEventType.VolunteerTaskUpdates => NotificationCategory.Volunteer,
            NotificationEventType.SavedSearchMatches => NotificationCategory.SavedSearch,
            _ => NotificationCategory.System
        };
    }

    private static string NormalizeError(string value)
    {
        var normalized = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private enum ProcessOneResult
    {
        Sent,
        Failed,
        DeadLettered,
        Skipped
    }

    private enum DeliveryOutcome
    {
        Sent,
        Cancelled
    }
}


