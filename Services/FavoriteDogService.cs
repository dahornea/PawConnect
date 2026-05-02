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
            .ThenInclude(d => d!.Shelter)
            .Where(f => f.AdopterId == userId)
            .AsNoTracking()
            .ToListAsync();
    }
}
