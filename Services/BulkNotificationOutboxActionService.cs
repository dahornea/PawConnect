using PawConnect.Data;

namespace PawConnect.Services;

public sealed class BulkNotificationOutboxActionService(
    INotificationOutboxService outboxService,
    IAuditLogService auditLogService) : IBulkNotificationOutboxActionService
{
    private const int MaxBulkItems = 100;

    public Task<BulkActionResultDto> RetryAsync(
        string adminUserId,
        IReadOnlyCollection<int> outboxMessageIds,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(
            adminUserId,
            outboxMessageIds,
            "Retry selected notifications",
            id => outboxService.RetryAsync(id, adminUserId, cancellationToken),
            cancellationToken);
    }

    public Task<BulkActionResultDto> CancelAsync(
        string adminUserId,
        IReadOnlyCollection<int> outboxMessageIds,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(
            adminUserId,
            outboxMessageIds,
            "Cancel selected notifications",
            id => outboxService.CancelAsync(id, adminUserId, cancellationToken),
            cancellationToken);
    }

    private async Task<BulkActionResultDto> RunAsync(
        string adminUserId,
        IReadOnlyCollection<int> outboxMessageIds,
        string actionName,
        Func<int, Task> action,
        CancellationToken cancellationToken)
    {
        EnsureValidRequest(adminUserId, outboxMessageIds);
        var requestedIds = outboxMessageIds.Distinct().Take(MaxBulkItems + 1).ToList();
        if (requestedIds.Count > MaxBulkItems)
        {
            throw new InvalidOperationException($"Select {MaxBulkItems} notifications or fewer for one bulk action.");
        }

        var results = new List<BulkActionItemResultDto>();
        foreach (var id in requestedIds)
        {
            try
            {
                await action(id);
                results.Add(new BulkActionItemResultDto(
                    id,
                    $"Outbox #{id}",
                    BulkActionItemStatus.Succeeded,
                    "Action completed."));
            }
            catch (InvalidOperationException ex)
            {
                results.Add(new BulkActionItemResultDto(
                    id,
                    $"Outbox #{id}",
                    BulkActionItemStatus.Failed,
                    ex.Message));
            }
        }

        var summary = BulkActionResultDto.FromItems(actionName, results);
        await auditLogService.LogAsync(
            AuditActions.BulkNotificationOutboxUpdated,
            "NotificationOutbox",
            null,
            actionName,
            userId: adminUserId,
            userRole: IdentitySeedData.AdminRole,
            additionalData: $"Requested={summary.TotalRequested};Succeeded={summary.Succeeded};Failed={summary.Failed};Skipped={summary.Skipped}");

        return summary;
    }

    private static void EnsureValidRequest(string adminUserId, IReadOnlyCollection<int> ids)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }

        if (ids.Count == 0)
        {
            throw new InvalidOperationException("Select at least one outbox message before running a bulk action.");
        }
    }
}
