using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ResourceStockService(ApplicationDbContext context) : IResourceStockService
{
    public Task<List<ResourceStock>> GetAllAsync()
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<ResourceStock?> GetByIdAsync(int id)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task CreateAsync(ResourceStock resourceStock)
    {
        context.ResourceStocks.Add(resourceStock);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ResourceStock resourceStock)
    {
        resourceStock.LastUpdatedAt = DateTime.UtcNow;
        context.ResourceStocks.Update(resourceStock);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var resourceStock = await context.ResourceStocks.FindAsync(id);
        if (resourceStock is null)
        {
            return;
        }

        context.ResourceStocks.Remove(resourceStock);
        await context.SaveChangesAsync();
    }

    public Task<List<ResourceStock>> GetForShelterAsync(int shelterId)
    {
        return context.ResourceStocks
            .Where(r => r.ShelterId == shelterId)
            .AsNoTracking()
            .ToListAsync();
    }
}
