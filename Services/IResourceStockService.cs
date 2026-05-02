using PawConnect.Entities;

namespace PawConnect.Services;

public interface IResourceStockService
{
    Task<List<ResourceStock>> GetAllAsync();

    Task<ResourceStock?> GetByIdAsync(int id);

    Task CreateAsync(ResourceStock resourceStock);

    Task UpdateAsync(ResourceStock resourceStock);

    Task DeleteAsync(int id);

    Task<List<ResourceStock>> GetForShelterAsync(int shelterId);
}
