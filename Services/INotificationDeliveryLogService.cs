namespace PawConnect.Services;

public interface INotificationDeliveryLogService
{
    Task LogDeliveryAsync(
        NotificationDeliveryLogCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDeliveryLogDto>> GetAdminDeliveryLogsAsync(
        NotificationDeliveryLogFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<NotificationDeliverySummaryDto> GetAdminDeliverySummaryAsync(
        NotificationDeliveryLogFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default);
}
