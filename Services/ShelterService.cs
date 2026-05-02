using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ShelterService(ApplicationDbContext context) : IShelterService
{
    public Task<List<Shelter>> GetAllAsync()
    {
        return GetAllSheltersAsync();
    }

    public Task<Shelter?> GetByIdAsync(int id)
    {
        return context.Shelters
            .Include(s => s.Dogs)
            .Include(s => s.ResourceStocks)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task CreateAsync(Shelter shelter)
    {
        context.Shelters.Add(shelter);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Shelter shelter)
    {
        context.Shelters.Update(shelter);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var shelter = await context.Shelters.FindAsync(id);
        if (shelter is null)
        {
            return;
        }

        context.Shelters.Remove(shelter);
        await context.SaveChangesAsync();
    }

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
            .FirstOrDefaultAsync(s => s.ApplicationUserId == userId);
    }
}
