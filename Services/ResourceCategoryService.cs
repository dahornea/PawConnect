using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services.Caching;

namespace PawConnect.Services;

public class ResourceCategoryService(
    ApplicationDbContext context,
    ILocalCacheService? cache = null) : IResourceCategoryService
{
    private static readonly TimeSpan LookupCacheTtl = TimeSpan.FromMinutes(30);

    public Task<List<ResourceCategory>> GetAllAsync()
    {
        return GetAllResourceCategoriesAsync();
    }

    public async Task<List<ResourceCategory>> GetAllResourceCategoriesAsync()
    {
        if (cache is null)
        {
            return await QueryAllResourceCategoriesAsync();
        }

        var categories = await cache.GetOrCreateAsync(
            CacheKeys.ResourceCategoriesAll,
            QueryAllResourceCategoriesAsync,
            LookupCacheTtl);

        return categories.ToList();
    }

    private Task<List<ResourceCategory>> QueryAllResourceCategoriesAsync()
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
        cache?.Remove(CacheKeys.ResourceCategoriesAll);
    }

    public async Task UpdateAsync(ResourceCategory resourceCategory)
    {
        context.ResourceCategories.Update(resourceCategory);
        await context.SaveChangesAsync();
        cache?.Remove(CacheKeys.ResourceCategoriesAll);
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
        cache?.Remove(CacheKeys.ResourceCategoriesAll);
    }
}
