using PawConnect.Entities;

namespace PawConnect.Services;

public interface INotificationService
{
    Task CreateNotificationAsync(
        string userId,
        string title,
        string message,
        NotificationCategory category,
        NotificationType type,
        string? link = null,
        string? relatedEntityName = null,
        string? relatedEntityId = null,
        TimeSpan? suppressDuplicatesWithin = null);

    Task<List<Notification>> GetNotificationsForUserAsync(string userId, int count = 20);

    Task<List<Notification>> GetNotificationsForUserAsync(
        string userId,
        NotificationCategory? category,
        bool unreadOnly,
        int count = 100);

    Task<int> GetUnreadCountAsync(string userId);

    Task MarkAsReadAsync(int notificationId, string userId);

    Task MarkAllAsReadAsync(string userId);

    Task DeleteNotificationAsync(int notificationId, string userId);
}
