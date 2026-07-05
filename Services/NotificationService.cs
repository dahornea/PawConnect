using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class NotificationService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    ILogger<NotificationService> logger,
    INotificationPreferenceService? preferenceService = null,
    INotificationDeliveryLogService? deliveryLogService = null,
    INotificationOutboxService? outboxService = null) : INotificationService
{
    public async Task CreateNotificationAsync(
        string userId,
        string title,
        string message,
        NotificationCategory category,
        NotificationType type,
        string? link = null,
        string? relatedEntityName = null,
        string? relatedEntityId = null,
        TimeSpan? suppressDuplicatesWithin = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var normalizedTitle = NormalizeRequired(title, 120);
            var normalizedMessage = NormalizeRequired(message, 500);
            var now = DateTime.UtcNow;
            var notificationEventType = NotificationEventTypeMapper.FromNotification(category, relatedEntityName, normalizedTitle);
            var recipient = await context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync();

            if (preferenceService is not null &&
                !await preferenceService.IsChannelEnabledAsync(userId, notificationEventType, NotificationChannel.InApp))
            {
                await TryEnqueueEmailOutboxAsync(
                    userId,
                    recipient,
                    notificationEventType,
                    normalizedTitle,
                    normalizedMessage,
                    link,
                    relatedEntityName,
                    relatedEntityId);

                await TryLogDeliveryAsync(new NotificationDeliveryLogCreateRequest(
                    notificationEventType,
                    NotificationChannel.InApp,
                    NotificationDeliveryStatus.DisabledByPreference,
                    UserId: userId,
                    Recipient: recipient,
                    Subject: normalizedTitle,
                    RelatedEntityType: relatedEntityName,
                    RelatedEntityId: relatedEntityId));
                return;
            }

            if (suppressDuplicatesWithin is { } suppressionWindow && suppressionWindow > TimeSpan.Zero)
            {
                var cutoff = now.Subtract(suppressionWindow);
                var duplicateExists = await context.Notifications.AnyAsync(notification =>
                    notification.UserId == userId
                    && notification.Category == category
                    && notification.Title == normalizedTitle
                    && notification.Message == normalizedMessage
                    && notification.CreatedAt >= cutoff);

                if (duplicateExists)
                {
                    logger.LogInformation(
                        "Skipped duplicate notification for user {UserId}, category {Category}, title {Title}.",
                        userId,
                        category,
                        normalizedTitle);
                    await TryLogDeliveryAsync(new NotificationDeliveryLogCreateRequest(
                        notificationEventType,
                        NotificationChannel.InApp,
                        NotificationDeliveryStatus.Skipped,
                        UserId: userId,
                        Recipient: recipient,
                        Subject: normalizedTitle,
                        ErrorMessage: "Suppressed duplicate notification.",
                        RelatedEntityType: relatedEntityName,
                        RelatedEntityId: relatedEntityId));
                    return;
                }
            }

            var notification = new Notification
            {
                UserId = userId,
                Title = normalizedTitle,
                Message = normalizedMessage,
                Category = category,
                Type = type,
                Link = NormalizeOptional(link, 300),
                RelatedEntityName = NormalizeOptional(relatedEntityName, 80),
                RelatedEntityId = NormalizeOptional(relatedEntityId, 80),
                CreatedAt = now
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync();

            await TryLogDeliveryAsync(new NotificationDeliveryLogCreateRequest(
                notificationEventType,
                NotificationChannel.InApp,
                NotificationDeliveryStatus.Sent,
                NotificationId: notification.Id,
                UserId: userId,
                Recipient: recipient,
                Subject: normalizedTitle,
                SentAt: now,
                RelatedEntityType: relatedEntityName,
                RelatedEntityId: relatedEntityId));

            await TryEnqueueEmailOutboxAsync(
                userId,
                recipient,
                notificationEventType,
                normalizedTitle,
                normalizedMessage,
                link,
                relatedEntityName,
                relatedEntityId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification creation failed for user {UserId} and category {Category}.", userId, category);
        }
    }

    public async Task<List<Notification>> GetNotificationsForUserAsync(string userId, int count = 20)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await GetNotificationsQuery(context, userId)
            .Take(Math.Clamp(count, 1, 200))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Notification>> GetNotificationsForUserAsync(
        string userId,
        NotificationCategory? category,
        bool unreadOnly,
        int count = 100)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        IQueryable<Notification> query = GetNotificationsQuery(context, userId);

        if (category.HasValue)
        {
            query = query.Where(notification => notification.Category == category.Value);
        }

        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        return await query
            .Take(Math.Clamp(count, 1, 500))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Notifications.CountAsync(notification => notification.UserId == userId && !notification.IsRead);
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification is null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var notifications = await context.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteNotificationAsync(int notificationId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification is null)
        {
            return;
        }

        context.Notifications.Remove(notification);
        await context.SaveChangesAsync();
    }

    private static IQueryable<Notification> GetNotificationsQuery(ApplicationDbContext context, string userId)
    {
        return context.Notifications
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id);
    }

    private static string NormalizeRequired(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Notification" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private async Task TryLogDeliveryAsync(NotificationDeliveryLogCreateRequest request)
    {
        if (deliveryLogService is null)
        {
            return;
        }

        try
        {
            await deliveryLogService.LogDeliveryAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification delivery log creation failed for user {UserId}.", request.UserId);
        }
    }

    private async Task TryEnqueueEmailOutboxAsync(
        string userId,
        string? recipient,
        NotificationEventType notificationEventType,
        string subject,
        string body,
        string? link,
        string? relatedEntityName,
        string? relatedEntityId)
    {
        if (outboxService is null || string.IsNullOrWhiteSpace(recipient))
        {
            return;
        }

        try
        {
            if (preferenceService is not null &&
                !await preferenceService.IsChannelEnabledAsync(userId, notificationEventType, NotificationChannel.Email))
            {
                return;
            }

            await outboxService.EnqueueAsync(new NotificationOutboxCreateRequest(
                notificationEventType,
                NotificationChannel.Email,
                subject,
                body,
                RecipientUserId: userId,
                RecipientEmail: recipient,
                Link: link,
                RelatedEntityType: relatedEntityName,
                RelatedEntityId: relatedEntityId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification email outbox enqueue failed for user {UserId}.", userId);
        }
    }

}
