using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services.Caching;

namespace PawConnect.Services;

public class DogBreedService(
    ApplicationDbContext context,
    ILocalCacheService? cache = null) : IDogBreedService
{
    private static readonly TimeSpan LookupCacheTtl = TimeSpan.FromMinutes(30);

    public async Task<List<DogBreed>> GetActiveBreedsAsync()
    {
        if (cache is null)
        {
            return await QueryActiveBreedsAsync();
        }

        var breeds = await cache.GetOrCreateAsync(
            CacheKeys.ActiveDogBreeds,
            QueryActiveBreedsAsync,
            LookupCacheTtl);

        return breeds.ToList();
    }

    private Task<List<DogBreed>> QueryActiveBreedsAsync()
    {
        return context.DogBreeds
            .Where(breed => breed.IsActive)
            .OrderBy(breed => breed.Name)
            .AsNoTracking()
            .ToListAsync();
    }
}
