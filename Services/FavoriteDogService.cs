using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class FavoriteDogService(ApplicationDbContext context) : IFavoriteDogService
{
    public Task<List<FavoriteDog>> GetFavoritesForUserAsync(string userId)
    {
        return context.FavoriteDogs
            .Include(f => f.Dog)
            .ThenInclude(d => d!.Shelter)
            .Where(f => f.UserId == userId)
            .AsNoTracking()
            .ToListAsync();
    }
}
