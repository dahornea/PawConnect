namespace PawConnect.Services;

public interface INotificationOutboxService
{
    Task<NotificationOutboxMessageDto> EnqueueAsync(
        NotificationOutboxCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationOutboxMessageDto>> EnqueueManyAsync(
        IEnumerable<NotificationOutboxCreateRequest> requests,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationOutboxMessageDto>> GetAdminMessagesAsync(
        NotificationOutboxFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<NotificationOutboxSummaryDto> GetAdminSummaryAsync(
        NotificationOutboxFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<NotificationOutboxMessageDto?> GetAdminMessageAsync(
        int outboxMessageId,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task RetryAsync(
        int outboxMessageId,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        int outboxMessageId,
        string adminUserId,
        CancellationToken cancellationToken = default);
}
