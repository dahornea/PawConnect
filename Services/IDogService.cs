using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogService
{
    Task<List<Dog>> GetAllAsync();

    Task<Dog?> GetByIdAsync(int id);

    Task CreateAsync(Dog dog);

    Task UpdateAsync(Dog dog);

    Task DeleteAsync(int id);

    Task<List<Dog>> GetAvailableDogsAsync();

    Task<List<Dog>> GetAllDogsAsync();

    Task<Dog?> GetDogByIdAsync(int id);

    Task CreateDogAsync(Dog dog);
}
