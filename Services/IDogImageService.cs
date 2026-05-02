using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogImageService
{
    Task<List<DogImage>> GetAllAsync();

    Task<DogImage?> GetByIdAsync(int id);

    Task CreateAsync(DogImage dogImage);

    Task UpdateAsync(DogImage dogImage);

    Task DeleteAsync(int id);
}
