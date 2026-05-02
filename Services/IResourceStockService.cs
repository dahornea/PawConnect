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

    Task<List<ResourceStock>> GetResourcesForShelterAsync(int shelterId);

    Task<List<ResourceStock>> GetLowStockResourcesForShelterAsync(int shelterId);

    Task<ResourceStock?> GetResourceForShelterAsync(int resourceId, int shelterId);

    Task CreateResourceAsync(ResourceStock resource, int shelterId);

    Task UpdateResourceAsync(ResourceStock resource, int shelterId);

    Task DeleteResourceAsync(int resourceId, int shelterId);
}
