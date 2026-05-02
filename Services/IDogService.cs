using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogService
{
    Task<List<Dog>> GetAvailableDogsAsync();

    Task<List<Dog>> GetAllDogsAsync();

    Task<Dog?> GetDogByIdAsync(int id);

    Task CreateDogAsync(Dog dog);
}
