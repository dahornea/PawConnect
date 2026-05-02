using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ShelterService(ApplicationDbContext context) : IShelterService
{
    public Task<List<Shelter>> GetAllSheltersAsync()
    {
        return context.Shelters
            .Include(s => s.Dogs)
            .Include(s => s.ResourceStocks)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<Shelter?> GetShelterForUserAsync(string userId)
    {
        return context.Shelters
            .Include(s => s.Dogs)
            .Include(s => s.ResourceStocks)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OwnerUserId == userId);
    }
}
