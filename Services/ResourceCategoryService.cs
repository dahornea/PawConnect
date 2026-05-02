using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ResourceCategoryService(ApplicationDbContext context) : IResourceCategoryService
{
    public Task<List<ResourceCategory>> GetAllAsync()
    {
        return GetAllResourceCategoriesAsync();
    }

    public Task<List<ResourceCategory>> GetAllResourceCategoriesAsync()
    {
        return context.ResourceCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public Task<ResourceCategory?> GetByIdAsync(int id)
    {
        return context.ResourceCategories
            .Include(c => c.ResourceStocks)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task CreateAsync(ResourceCategory resourceCategory)
    {
        context.ResourceCategories.Add(resourceCategory);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ResourceCategory resourceCategory)
    {
        context.ResourceCategories.Update(resourceCategory);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var resourceCategory = await context.ResourceCategories.FindAsync(id);
        if (resourceCategory is null)
        {
            return;
        }

        context.ResourceCategories.Remove(resourceCategory);
        await context.SaveChangesAsync();
    }
}
