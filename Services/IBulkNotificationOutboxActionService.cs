namespace PawConnect.Services;

public interface IBulkNotificationOutboxActionService
{
    Task<BulkActionResultDto> RetryAsync(
        string adminUserId,
        IReadOnlyCollection<int> outboxMessageIds,
        CancellationToken cancellationToken = default);

    Task<BulkActionResultDto> CancelAsync(
        string adminUserId,
        IReadOnlyCollection<int> outboxMessageIds,
        CancellationToken cancellationToken = default);
}
