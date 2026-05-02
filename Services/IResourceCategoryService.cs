using PawConnect.Entities;

namespace PawConnect.Services;

public interface IResourceCategoryService
{
    Task<List<ResourceCategory>> GetAllAsync();

    Task<ResourceCategory?> GetByIdAsync(int id);

    Task CreateAsync(ResourceCategory resourceCategory);

    Task UpdateAsync(ResourceCategory resourceCategory);

    Task DeleteAsync(int id);
}
