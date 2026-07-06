using Microsoft.Extensions.Caching.Memory;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Services.Caching;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class LocalCacheServiceTests
{
    [Fact]
    public void ScopedCacheKeys_SeparateUserAndShelterScopes()
    {
        var adopterKey = CacheKeys.Scoped("saved-searches", "user:adopter-1", "page", 1);
        var otherAdopterKey = CacheKeys.Scoped("saved-searches", "user:adopter-2", "page", 1);
        var shelterKey = CacheKeys.Scoped("dashboard", "shelter:1", "summary");
        var otherShelterKey = CacheKeys.Scoped("dashboard", "shelter:2", "summary");

        Assert.NotEqual(adopterKey, otherAdopterKey);
        Assert.NotEqual(shelterKey, otherShelterKey);
        Assert.Contains("user:adopter-1", adopterKey);
        Assert.Contains("shelter:1", shelterKey);
    }

    [Fact]
    public async Task ResourceCategoryService_InvalidatesCachedLookupAfterCreate()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var cache = new LocalCacheService(new MemoryCache(new MemoryCacheOptions()));
        var service = new ResourceCategoryService(context, cache);

        var beforeCreate = await service.GetAllResourceCategoriesAsync();

        await service.CreateAsync(new ResourceCategory
        {
            Name = "Training Supplies",
            Description = "Treat pouches and clickers."
        });

        var afterCreate = await service.GetAllResourceCategoriesAsync();

        Assert.DoesNotContain(beforeCreate, category => category.Name == "Training Supplies");
        Assert.Contains(afterCreate, category => category.Name == "Training Supplies");
    }
}
