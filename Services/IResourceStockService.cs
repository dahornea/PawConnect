using PawConnect.Entities;

namespace PawConnect.Services;

public interface IResourceStockService
{
    Task<List<ResourceStock>> GetForShelterAsync(int shelterId);
}
