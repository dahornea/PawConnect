using PawConnect.Entities;

namespace PawConnect.Services;

public interface IBulkDogActionService
{
    Task<BulkActionResultDto> UpdateShelterDogStatusAsync(
        int shelterId,
        string actorUserId,
        IReadOnlyCollection<int> dogIds,
        DogStatus newStatus,
        CancellationToken cancellationToken = default);
}
