using PawConnect.Entities;

namespace PawConnect.Services;

public interface INotificationCenterService
{
    Task<NotificationCenterResultDto> GetNotificationsAsync(
        string userId,
        NotificationCenterQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationCenterItemDto>> GetPreviewAsync(
        string userId,
        int count = 8,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        int notificationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task MarkAsUnreadAsync(
        int notificationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task DismissAsync(
        int notificationId,
        string userId,
        CancellationToken cancellationToken = default);
}
