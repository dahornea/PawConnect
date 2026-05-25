using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogBreedService
{
    Task<List<DogBreed>> GetActiveBreedsAsync();
}
