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
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<ResourceStock?> GetByIdAsync(int id)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
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
        return GetResourcesForShelterAsync(shelterId);
    }

    public Task<List<ResourceStock>> GetResourcesForShelterAsync(int shelterId)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .Where(r => r.ShelterId == shelterId)
            .OrderBy(r => r.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<ResourceStock>> GetLowStockResourcesForShelterAsync(int shelterId)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .Where(r => r.ShelterId == shelterId && r.Quantity <= r.LowStockThreshold)
            .OrderBy(r => r.Quantity)
            .ThenBy(r => r.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<ResourceStock?> GetResourceForShelterAsync(int resourceId, int shelterId)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resourceId && r.ShelterId == shelterId);
    }

    public async Task CreateResourceAsync(ResourceStock resource, int shelterId)
    {
        await PrepareResourceAsync(resource);

        resource.Id = 0;
        resource.ShelterId = shelterId;
        resource.Shelter = null;
        resource.LastUpdatedAt = DateTime.UtcNow;

        context.ResourceStocks.Add(resource);
        await context.SaveChangesAsync();
    }

    public async Task UpdateResourceAsync(ResourceStock resource, int shelterId)
    {
        await PrepareResourceAsync(resource);

        var existingResource = await context.ResourceStocks.FirstOrDefaultAsync(r => r.Id == resource.Id && r.ShelterId == shelterId);
        if (existingResource is null)
        {
            throw new InvalidOperationException("Resource stock item was not found for your shelter.");
        }

        existingResource.Name = resource.Name.Trim();
        existingResource.ResourceCategoryId = resource.ResourceCategoryId;
        existingResource.FoodTypeId = resource.FoodTypeId;
        existingResource.Quantity = resource.Quantity;
        existingResource.Unit = resource.Unit.Trim();
        existingResource.LowStockThreshold = resource.LowStockThreshold;
        existingResource.LastUpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task DeleteResourceAsync(int resourceId, int shelterId)
    {
        var resource = await context.ResourceStocks.FirstOrDefaultAsync(r => r.Id == resourceId && r.ShelterId == shelterId);
        if (resource is null)
        {
            throw new InvalidOperationException("Resource stock item was not found for your shelter.");
        }

        context.ResourceStocks.Remove(resource);
        await context.SaveChangesAsync();
    }

    private async Task PrepareResourceAsync(ResourceStock resource)
    {
        ValidateResource(resource);

        var category = await context.ResourceCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == resource.ResourceCategoryId);
        if (category is null)
        {
            throw new InvalidOperationException("Resource category is required.");
        }

        if (!category.Name.Equals("Food", StringComparison.OrdinalIgnoreCase))
        {
            resource.FoodTypeId = null;
        }
        else if (resource.FoodTypeId.HasValue)
        {
            var foodTypeExists = await context.FoodTypes.AnyAsync(f => f.Id == resource.FoodTypeId.Value);
            if (!foodTypeExists)
            {
                throw new InvalidOperationException("Selected food type was not found.");
            }
        }

        resource.Name = resource.Name.Trim();
        resource.Unit = resource.Unit.Trim();
    }

    private static void ValidateResource(ResourceStock resource)
    {
        if (string.IsNullOrWhiteSpace(resource.Name))
        {
            throw new InvalidOperationException("Resource name is required.");
        }

        if (resource.ResourceCategoryId <= 0)
        {
            throw new InvalidOperationException("Resource category is required.");
        }

        if (resource.Quantity < 0)
        {
            throw new InvalidOperationException("Quantity must be zero or greater.");
        }

        if (resource.LowStockThreshold < 0)
        {
            throw new InvalidOperationException("Low-stock threshold must be zero or greater.");
        }

        if (string.IsNullOrWhiteSpace(resource.Unit))
        {
            throw new InvalidOperationException("Unit is required.");
        }
    }
}
