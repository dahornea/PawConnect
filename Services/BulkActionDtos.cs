using PawConnect.Entities;

namespace PawConnect.Services;

public enum BulkActionItemStatus
{
    Succeeded = 0,
    Failed = 1,
    Skipped = 2
}

public sealed record BulkActionItemResultDto(
    int EntityId,
    string EntityName,
    BulkActionItemStatus Status,
    string? Message = null);

public sealed record BulkActionResultDto(
    int TotalRequested,
    int Succeeded,
    int Failed,
    int Skipped,
    string Message,
    IReadOnlyList<BulkActionItemResultDto> Items)
{
    public static BulkActionResultDto FromItems(string actionName, IReadOnlyList<BulkActionItemResultDto> items)
    {
        var succeeded = items.Count(item => item.Status == BulkActionItemStatus.Succeeded);
        var failed = items.Count(item => item.Status == BulkActionItemStatus.Failed);
        var skipped = items.Count(item => item.Status == BulkActionItemStatus.Skipped);
        var message = $"{actionName}: {succeeded} succeeded, {failed} failed, {skipped} skipped.";

        return new BulkActionResultDto(items.Count, succeeded, failed, skipped, message, items);
    }
}

public sealed record BulkDogStatusUpdateRequest(
    IReadOnlyCollection<int> DogIds,
    DogStatus NewStatus);

public sealed record BulkNotificationOutboxRequest(
    IReadOnlyCollection<int> MessageIds,
    string Action);
