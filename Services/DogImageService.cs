using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogImageService(ApplicationDbContext context) : IDogImageService
{
    public Task<List<DogImage>> GetAllAsync()
    {
        return context.DogImages
            .Include(i => i.Dog)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<DogImage?> GetByIdAsync(int id)
    {
        return context.DogImages
            .Include(i => i.Dog)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task CreateAsync(DogImage dogImage)
    {
        context.DogImages.Add(dogImage);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(DogImage dogImage)
    {
        context.DogImages.Update(dogImage);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var dogImage = await context.DogImages.FindAsync(id);
        if (dogImage is null)
        {
            return;
        }

        context.DogImages.Remove(dogImage);
        await context.SaveChangesAsync();
    }
}
