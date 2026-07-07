using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogProfileCompletenessService
{
    DogProfileCompletenessDto CalculateForDog(Dog dog);

    Task<DogProfileCompletenessDto> CalculateForShelterDogAsync(
        int dogId,
        int shelterId,
        CancellationToken cancellationToken = default);

    Task<DogProfileCompletenessDto> CalculateForAdminDogAsync(
        int dogId,
        CancellationToken cancellationToken = default);

    IReadOnlyDictionary<int, DogProfileCompletenessDto> CalculateForDogs(IEnumerable<Dog> dogs);

    Task<DogProfileCompletenessSummaryDto> GetShelterCompletenessSummaryAsync(
        int shelterId,
        CancellationToken cancellationToken = default);

    Task<DogProfileCompletenessSummaryDto> GetAdminCompletenessStatsAsync(
        CancellationToken cancellationToken = default);
}
