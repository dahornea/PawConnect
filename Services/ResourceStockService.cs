using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ResourceStockService(ApplicationDbContext context) : IResourceStockService
{
    public Task<List<ResourceStock>> GetForShelterAsync(int shelterId)
    {
        return context.ResourceStocks
            .Where(r => r.ShelterId == shelterId)
            .AsNoTracking()
            .ToListAsync();
    }
}
