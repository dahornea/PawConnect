using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class RecentlyViewedDogService(ApplicationDbContext context) : IRecentlyViewedDogService
{
    private const int MaxRecentViewsPerAdopter = 20;

    public async Task TrackViewAsync(string adopterId, int dogId)
    {
        if (string.IsNullOrWhiteSpace(adopterId))
        {
            return;
        }

        var dog = await context.Dogs
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dogId);

        if (dog is null || dog.Status is not (DogStatus.Available or DogStatus.Reserved))
        {
            return;
        }

        var existingView = await context.RecentlyViewedDogs
            .FirstOrDefaultAsync(v => v.AdopterId == adopterId && v.DogId == dogId);

        if (existingView is null)
        {
            context.RecentlyViewedDogs.Add(new RecentlyViewedDog
            {
                AdopterId = adopterId,
                DogId = dogId,
                ViewedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingView.ViewedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        await TrimOldRecentViewsAsync(adopterId);
    }

    public Task<List<RecentlyViewedDog>> GetRecentlyViewedDogsAsync(string adopterId, int count)
    {
        return context.RecentlyViewedDogs
            .Include(v => v.Dog)
            .ThenInclude(d => d!.Images)
            .Include(v => v.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(v => v.Dog)
            .ThenInclude(d => d!.PreferredFoodType)
            .Where(v =>
                v.AdopterId == adopterId &&
                v.Dog != null &&
                (v.Dog.Status == DogStatus.Available || v.Dog.Status == DogStatus.Reserved))
            .OrderByDescending(v => v.ViewedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task ClearRecentlyViewedAsync(string adopterId)
    {
        var views = await context.RecentlyViewedDogs
            .Where(v => v.AdopterId == adopterId)
            .ToListAsync();

        if (views.Count == 0)
        {
            return;
        }

        context.RecentlyViewedDogs.RemoveRange(views);
        await context.SaveChangesAsync();
    }

    private async Task TrimOldRecentViewsAsync(string adopterId)
    {
        var oldViews = await context.RecentlyViewedDogs
            .Where(v => v.AdopterId == adopterId)
            .OrderByDescending(v => v.ViewedAt)
            .Skip(MaxRecentViewsPerAdopter)
            .ToListAsync();

        if (oldViews.Count == 0)
        {
            return;
        }

        context.RecentlyViewedDogs.RemoveRange(oldViews);
        await context.SaveChangesAsync();
    }
}
