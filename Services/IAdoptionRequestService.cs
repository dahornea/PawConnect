using PawConnect.Entities;

namespace PawConnect.Services;

public interface IAdoptionRequestService
{
    Task<List<AdoptionRequest>> GetAllAsync();

    Task<List<AdoptionRequest>> GetForAdopterAsync(string userId);
}
