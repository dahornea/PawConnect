using Microsoft.AspNetCore.Components.Forms;

namespace PawConnect.Services;

public interface IDogImageStorageService
{
    Task<DogImageUploadResult> SaveDogImageAsync(
        int dogId,
        IBrowserFile file,
        CancellationToken cancellationToken = default);

    Task DeleteDogImageAsync(
        string imagePathOrKey,
        CancellationToken cancellationToken = default);
}
