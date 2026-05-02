using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class FavoriteDogService(ApplicationDbContext context) : IFavoriteDogService
{
    public Task<List<FavoriteDog>> GetAllAsync()
    {
        return context.FavoriteDogs
            .Include(f => f.Dog)
            .Include(f => f.Adopter)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<FavoriteDog?> GetByIdAsync(int id)
    {
        return context.FavoriteDogs
            .Include(f => f.Dog)
            .Include(f => f.Adopter)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task CreateAsync(FavoriteDog favoriteDog)
    {
        context.FavoriteDogs.Add(favoriteDog);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(FavoriteDog favoriteDog)
    {
        context.FavoriteDogs.Update(favoriteDog);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var favoriteDog = await context.FavoriteDogs.FindAsync(id);
        if (favoriteDog is null)
        {
            return;
        }

        context.FavoriteDogs.Remove(favoriteDog);
        await context.SaveChangesAsync();
    }

    public Task<List<FavoriteDog>> GetFavoritesForUserAsync(string userId)
    {
        return context.FavoriteDogs
            .Include(f => f.Dog)
            .ThenInclude(d => d!.Images)
            .Include(f => f.Dog)
            .ThenInclude(d => d!.Shelter)
            .Where(f => f.AdopterId == userId)
            .OrderBy(f => f.Dog!.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<HashSet<int>> GetFavoriteDogIdsForUserAsync(string adopterId)
    {
        var dogIds = await context.FavoriteDogs
            .Where(f => f.AdopterId == adopterId)
            .Select(f => f.DogId)
            .ToListAsync();

        return dogIds.ToHashSet();
    }

    public Task<bool> IsFavoriteAsync(string adopterId, int dogId)
    {
        return context.FavoriteDogs.AnyAsync(f => f.AdopterId == adopterId && f.DogId == dogId);
    }

    public async Task AddFavoriteAsync(string adopterId, int dogId)
    {
        var dog = await context.Dogs
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dogId);

        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found.");
        }

        if (dog.Status is not (DogStatus.Available or DogStatus.Reserved))
        {
            throw new InvalidOperationException("Only available or reserved dogs can be added to favorites.");
        }

        var alreadyFavorite = await IsFavoriteAsync(adopterId, dogId);
        if (alreadyFavorite)
        {
            return;
        }

        context.FavoriteDogs.Add(new FavoriteDog
        {
            AdopterId = adopterId,
            DogId = dogId
        });

        await context.SaveChangesAsync();
    }

    public async Task RemoveFavoriteAsync(string adopterId, int dogId)
    {
        var favorite = await context.FavoriteDogs
            .FirstOrDefaultAsync(f => f.AdopterId == adopterId && f.DogId == dogId);

        if (favorite is null)
        {
            return;
        }

        context.FavoriteDogs.Remove(favorite);
        await context.SaveChangesAsync();
    }
}
