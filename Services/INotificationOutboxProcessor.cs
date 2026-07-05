namespace PawConnect.Services;

public interface INotificationOutboxProcessor
{
    Task<NotificationOutboxProcessResult> ProcessDueAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
