using PawConnect.Entities;

namespace PawConnect.Services;

public interface IShelterService
{
    Task<List<Shelter>> GetAllSheltersAsync();

    Task<Shelter?> GetShelterForUserAsync(string userId);
}
