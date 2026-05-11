using PawConnect.Entities;

namespace PawConnect.Services;

public interface IShelterService
{
    Task<List<Shelter>> GetAllAsync();

    Task<Shelter?> GetByIdAsync(int id);

    Task<Shelter?> GetPublicShelterDetailsAsync(int id);

    Task CreateAsync(Shelter shelter);

    Task UpdateAsync(Shelter shelter);

    Task DeleteAsync(int id);

    Task<List<Shelter>> GetAllSheltersAsync();

    Task<Shelter?> GetShelterForUserAsync(string userId);

    Task UpdateShelterProfileAsync(Shelter shelter);
}
