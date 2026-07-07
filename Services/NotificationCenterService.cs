using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public sealed class NotificationCenterService(IDbContextFactory<ApplicationDbContext> contextFactory)
    : INotificationCenterService
{
    public async Task<NotificationCenterResultDto> GetNotificationsAsync(
        string userId,
        NotificationCenterQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new NotificationCenterResultDto([], 0, 0, []);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var baseQuery = UserNotifications(context, userId);
        var availableCategories = await baseQuery
            .Select(notification => notification.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync(cancellationToken);

        var filteredQuery = ApplyFilters(baseQuery, query);
        var count = Math.Clamp(query.Count, 1, 500);
        var notifications = await filteredQuery
            .Take(count)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var unreadCount = await baseQuery.CountAsync(notification => !notification.IsRead, cancellationToken);
        var items = notifications.Select(ToDto).ToList();

        return new NotificationCenterResultDto(
            GroupByTime(items),
            items.Count,
            unreadCount,
            availableCategories);
    }

    public async Task<IReadOnlyList<NotificationCenterItemDto>> GetPreviewAsync(
        string userId,
        int count = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var notifications = await UserNotifications(context, userId)
            .Take(Math.Clamp(count, 1, 20))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return notifications.Select(ToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Notifications.CountAsync(
            notification => notification.UserId == userId && !notification.IsRead,
            cancellationToken);
    }

    public async Task MarkAsReadAsync(
        int notificationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await UpdateReadStateAsync(notificationId, userId, isRead: true, cancellationToken);
    }

    public async Task MarkAsUnreadAsync(
        int notificationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await UpdateReadStateAsync(notificationId, userId, isRead: false, cancellationToken);
    }

    public async Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var notifications = await context.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DismissAsync(
        int notificationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var notification = await context.Notifications
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId, cancellationToken);

        if (notification is null)
        {
            return;
        }

        context.Notifications.Remove(notification);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Notification> ApplyFilters(
        IQueryable<Notification> query,
        NotificationCenterQuery filter)
    {
        if (filter.Category.HasValue)
        {
            query = query.Where(notification => notification.Category == filter.Category.Value);
        }

        query = filter.ReadState switch
        {
            NotificationReadState.Unread => query.Where(notification => !notification.IsRead),
            NotificationReadState.Read => query.Where(notification => notification.IsRead),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            query = query.Where(notification =>
                notification.Title.Contains(search) ||
                notification.Message.Contains(search) ||
                (notification.RelatedEntityName != null && notification.RelatedEntityName.Contains(search)));
        }

        return query;
    }

    private async Task UpdateReadStateAsync(
        int notificationId,
        string userId,
        bool isRead,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var notification = await context.Notifications
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId, cancellationToken);

        if (notification is null || notification.IsRead == isRead)
        {
            return;
        }

        notification.IsRead = isRead;
        notification.ReadAt = isRead ? DateTime.UtcNow : null;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Notification> UserNotifications(ApplicationDbContext context, string userId)
    {
        return context.Notifications
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id);
    }

    private static IReadOnlyList<NotificationCenterGroupDto> GroupByTime(IReadOnlyList<NotificationCenterItemDto> items)
    {
        var order = new[] { "Today", "Yesterday", "This Week", "Older" };
        return items
            .GroupBy(item => item.TimeGroup)
            .OrderBy(group => Array.IndexOf(order, group.Key))
            .Select(group => new NotificationCenterGroupDto(group.Key, group.ToList()))
            .ToList();
    }

    private static NotificationCenterItemDto ToDto(Notification notification)
    {
        var relatedUrl = NormalizeLink(notification.Link);
        var relatedName = string.IsNullOrWhiteSpace(notification.RelatedEntityName)
            ? null
            : notification.RelatedEntityName.Trim();
        var relatedId = string.IsNullOrWhiteSpace(notification.RelatedEntityId)
            ? null
            : notification.RelatedEntityId.Trim();

        return new NotificationCenterItemDto(
            notification.Id,
            notification.Title,
            notification.Message,
            notification.Category,
            notification.Type,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt,
            relatedName,
            relatedId,
            BuildRelatedDisplayName(relatedName, relatedId),
            relatedUrl,
            GetCategoryLabel(notification.Category),
            GetSeverityLabel(notification.Type),
            GetCategoryIcon(notification.Category),
            string.IsNullOrWhiteSpace(relatedUrl) ? "View details" : "Open",
            BuildMetadataSummary(notification),
            GetTimeGroup(notification.CreatedAt),
            GetRelativeTime(notification.CreatedAt));
    }

    private static string? NormalizeLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var normalized = link.Trim();
        return normalized.StartsWith('/') && !normalized.StartsWith("//", StringComparison.Ordinal)
            ? normalized
            : null;
    }

    private static string? BuildRelatedDisplayName(string? relatedName, string? relatedId)
    {
        if (string.IsNullOrWhiteSpace(relatedName))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(relatedId)
            ? relatedName
            : $"{relatedName} #{relatedId}";
    }

    private static string BuildMetadataSummary(Notification notification)
    {
        var parts = new List<string> { GetCategoryLabel(notification.Category), GetSeverityLabel(notification.Type) };
        if (!string.IsNullOrWhiteSpace(notification.RelatedEntityName))
        {
            parts.Add(notification.RelatedEntityName);
        }

        return string.Join(" · ", parts);
    }

    public static string GetCategoryLabel(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.Adoption => "Adoption",
            NotificationCategory.ShelterApplication => "Shelter Applications",
            NotificationCategory.Resource => "Resources",
            NotificationCategory.Report => "Reports",
            NotificationCategory.System => "System",
            NotificationCategory.Transfer => "Transfers",
            NotificationCategory.Volunteer => "Volunteer Tasks",
            NotificationCategory.SavedSearch => "Saved Searches",
            _ => category.ToString()
        };
    }

    public static string GetCategoryIcon(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.Adoption => "assignment_turned_in",
            NotificationCategory.ShelterApplication => "business",
            NotificationCategory.Resource => "inventory_2",
            NotificationCategory.Report => "picture_as_pdf",
            NotificationCategory.System => "info",
            NotificationCategory.Transfer => "swap_horiz",
            NotificationCategory.Volunteer => "volunteer_activism",
            NotificationCategory.SavedSearch => "saved_search",
            _ => "notifications"
        };
    }

    public static string GetSeverityLabel(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => "Success",
            NotificationType.Warning => "Warning",
            NotificationType.Error => "Error",
            _ => "Info"
        };
    }

    private static string GetTimeGroup(DateTime createdAtUtc)
    {
        var localDate = createdAtUtc.ToLocalTime().Date;
        var today = DateTime.Now.Date;

        if (localDate == today)
        {
            return "Today";
        }

        if (localDate == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return localDate >= today.AddDays(-7) ? "This Week" : "Older";
    }

    private static string GetRelativeTime(DateTime createdAtUtc)
    {
        var elapsed = DateTime.UtcNow - createdAtUtc;
        if (elapsed.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (elapsed.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)Math.Round(elapsed.TotalMinutes));
            return $"{minutes} min ago";
        }

        if (elapsed.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)Math.Round(elapsed.TotalHours));
            return $"{hours} h ago";
        }

        if (elapsed.TotalDays < 7)
        {
            var days = Math.Max(1, (int)Math.Round(elapsed.TotalDays));
            return $"{days} d ago";
        }

        return createdAtUtc.ToLocalTime().ToString("dd MMM yyyy");
    }
}
