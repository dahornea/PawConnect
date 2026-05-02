using PawConnect.Entities;

namespace PawConnect.Services;

public interface IAdoptionRequestService
{
    Task<List<AdoptionRequest>> GetAllAsync();

    Task<AdoptionRequest?> GetByIdAsync(int id);

    Task CreateAsync(AdoptionRequest adoptionRequest);

    Task UpdateAsync(AdoptionRequest adoptionRequest);

    Task DeleteAsync(int id);

    Task<List<AdoptionRequest>> GetForAdopterAsync(string userId);
}
