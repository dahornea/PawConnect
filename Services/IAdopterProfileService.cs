using PawConnect.Entities;

namespace PawConnect.Services;

public interface IAdopterProfileService
{
    Task<AdopterProfile?> GetProfileForUserAsync(string userId);

    Task CreateOrUpdateProfileAsync(string userId, AdopterProfile profile);
}
