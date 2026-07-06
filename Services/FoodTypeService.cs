using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services.Caching;

namespace PawConnect.Services;

public class FoodTypeService(
    ApplicationDbContext context,
    ILocalCacheService? cache = null) : IFoodTypeService
{
    private static readonly TimeSpan LookupCacheTtl = TimeSpan.FromMinutes(30);

    public Task<List<FoodType>> GetAllAsync()
    {
        return GetAllFoodTypesAsync();
    }

    public async Task<List<FoodType>> GetAllFoodTypesAsync()
    {
        if (cache is null)
        {
            return await QueryAllFoodTypesAsync();
        }

        var foodTypes = await cache.GetOrCreateAsync(
            CacheKeys.FoodTypesAll,
            QueryAllFoodTypesAsync,
            LookupCacheTtl);

        return foodTypes.ToList();
    }

    private Task<List<FoodType>> QueryAllFoodTypesAsync()
    {
        return context.FoodTypes
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public Task<FoodType?> GetByIdAsync(int id)
    {
        return context.FoodTypes
            .Include(f => f.ResourceStocks)
            .Include(f => f.Dogs)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task CreateAsync(FoodType foodType)
    {
        context.FoodTypes.Add(foodType);
        await context.SaveChangesAsync();
        cache?.Remove(CacheKeys.FoodTypesAll);
    }

    public async Task UpdateAsync(FoodType foodType)
    {
        context.FoodTypes.Update(foodType);
        await context.SaveChangesAsync();
        cache?.Remove(CacheKeys.FoodTypesAll);
    }

    public async Task DeleteAsync(int id)
    {
        var foodType = await context.FoodTypes.FindAsync(id);
        if (foodType is null)
        {
            return;
        }

        context.FoodTypes.Remove(foodType);
        await context.SaveChangesAsync();
        cache?.Remove(CacheKeys.FoodTypesAll);
    }
}
