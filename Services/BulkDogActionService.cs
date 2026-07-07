using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public sealed class BulkDogActionService(
    ApplicationDbContext context,
    IAuditLogService auditLogService) : IBulkDogActionService
{
    private const int MaxBulkItems = 100;

    public async Task<BulkActionResultDto> UpdateShelterDogStatusAsync(
        int shelterId,
        string actorUserId,
        IReadOnlyCollection<int> dogIds,
        DogStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        EnsureValidRequest(actorUserId, dogIds);
        EnsureSafeTargetStatus(newStatus);

        var requestedIds = dogIds.Distinct().Take(MaxBulkItems + 1).ToList();
        if (requestedIds.Count > MaxBulkItems)
        {
            throw new InvalidOperationException($"Select {MaxBulkItems} dogs or fewer for one bulk action.");
        }

        var dogs = await context.Dogs
            .Where(dog => requestedIds.Contains(dog.Id))
            .ToDictionaryAsync(dog => dog.Id, cancellationToken);
        var now = DateTime.UtcNow;
        var results = new List<BulkActionItemResultDto>();

        foreach (var dogId in requestedIds)
        {
            if (!dogs.TryGetValue(dogId, out var dog) || dog.ShelterId != shelterId)
            {
                results.Add(new BulkActionItemResultDto(
                    dogId,
                    $"Dog #{dogId}",
                    BulkActionItemStatus.Failed,
                    "Dog was not found for your shelter."));
                continue;
            }

            if (dog.Status == DogStatus.Adopted)
            {
                results.Add(new BulkActionItemResultDto(
                    dog.Id,
                    dog.Name,
                    BulkActionItemStatus.Failed,
                    "Adopted dogs are read-only for shelter users."));
                continue;
            }

            if (dog.Status == newStatus)
            {
                results.Add(new BulkActionItemResultDto(
                    dog.Id,
                    dog.Name,
                    BulkActionItemStatus.Skipped,
                    $"Already marked as {newStatus}."));
                continue;
            }

            var oldStatus = dog.Status;
            dog.Status = newStatus;
            context.DogStatusHistories.Add(new DogStatusHistory
            {
                DogId = dog.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedAt = now,
                ChangedByUserId = actorUserId,
                Notes = "Status changed through shelter bulk action."
            });

            results.Add(new BulkActionItemResultDto(
                dog.Id,
                dog.Name,
                BulkActionItemStatus.Succeeded,
                $"Status changed from {oldStatus} to {newStatus}."));
        }

        await context.SaveChangesAsync(cancellationToken);
        var summary = BulkActionResultDto.FromItems($"Bulk dog status update to {newStatus}", results);
        await auditLogService.LogAsync(
            AuditActions.BulkDogStatusUpdated,
            "Dog",
            null,
            $"Shelter bulk dog status update to {newStatus}.",
            userId: actorUserId,
            userRole: IdentitySeedData.ShelterRole,
            additionalData: $"ShelterId={shelterId};Requested={summary.TotalRequested};Succeeded={summary.Succeeded};Failed={summary.Failed};Skipped={summary.Skipped}");

        return summary;
    }

    private static void EnsureValidRequest(string actorUserId, IReadOnlyCollection<int> dogIds)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }

        if (dogIds.Count == 0)
        {
            throw new InvalidOperationException("Select at least one dog before running a bulk action.");
        }
    }

    private static void EnsureSafeTargetStatus(DogStatus newStatus)
    {
        if (newStatus is not (DogStatus.Available or DogStatus.Reserved or DogStatus.InTreatment))
        {
            throw new InvalidOperationException("This dog status is not available as a bulk action.");
        }
    }
}
