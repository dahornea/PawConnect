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

    Task<List<Dog>> GetAdoptedDogsAsync();

    Task<List<Dog>> SearchDogsAsync(string? searchTerm, string? breed, int? maxAge, DogSize? size, string? location, DogStatus? status);

    Task<Dog?> GetDogDetailsAsync(int id);

    Task<List<Dog>> GetAllDogsAsync();

    Task<Dog?> GetDogByIdAsync(int id);

    Task CreateDogAsync(Dog dog);

    Task<List<Dog>> GetDogsForShelterAsync(int shelterId);

    Task<Dog?> GetDogForShelterAsync(int dogId, int shelterId);

    Task CreateDogAsync(Dog dog, int shelterId);

    Task UpdateDogAsync(Dog dog, int shelterId, string? changedByUserId = null);

    Task UpdateSuccessStoryAsync(int dogId, int shelterId, string? successStoryText, DateTime? adoptedAt);

    Task DeleteDogAsync(int dogId, int shelterId);

    Task<List<Dog>> GetAllDogsForAdminAsync();

    Task DeleteDogForAdminAsync(int dogId);

    Task<List<DogStatusHistory>> GetStatusHistoryForDogAsync(int dogId);

    Task<List<DogStatusHistory>> GetStatusHistoryForShelterDogAsync(int dogId, int shelterId);

    Task AddStatusHistoryAsync(int dogId, DogStatus oldStatus, DogStatus newStatus, string? changedByUserId, string? notes = null);
}
