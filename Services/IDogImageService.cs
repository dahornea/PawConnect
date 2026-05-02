using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogImageService
{
    Task<List<DogImage>> GetAllAsync();

    Task<DogImage?> GetByIdAsync(int id);

    Task CreateAsync(DogImage dogImage);

    Task UpdateAsync(DogImage dogImage);

    Task DeleteAsync(int id);

    Task<List<DogImage>> GetImagesForDogAsync(int dogId);

    Task AddDogImageAsync(int dogId, int shelterId, DogImage image);

    Task SetMainImageAsync(int imageId, int shelterId);

    Task DeleteDogImageAsync(int imageId, int shelterId);
}
