using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class FoodTypeService(ApplicationDbContext context) : IFoodTypeService
{
    public Task<List<FoodType>> GetAllAsync()
    {
        return context.FoodTypes
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public Task<FoodType?> GetByIdAsync(int id)
    {
        return context.FoodTypes
            .Include(f => f.ResourceStocks)
            .Include(f => f.Dogs)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task CreateAsync(FoodType foodType)
    {
        context.FoodTypes.Add(foodType);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(FoodType foodType)
    {
        context.FoodTypes.Update(foodType);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var foodType = await context.FoodTypes.FindAsync(id);
        if (foodType is null)
        {
            return;
        }

        context.FoodTypes.Remove(foodType);
        await context.SaveChangesAsync();
    }
}
