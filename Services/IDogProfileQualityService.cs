using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogProfileQualityService
{
    Task<DogProfileQualityResult> CheckFormAsync(
        DogProfileQualityRequest request,
        CancellationToken cancellationToken = default);

    Task<DogProfileQualityResult> CheckDogAsync(
        int dogId,
        int shelterId,
        CancellationToken cancellationToken = default);

    DogProfileQualityRequest BuildRequestFromDog(Dog dog, int shelterId);
}
