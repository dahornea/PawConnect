using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogService(ApplicationDbContext context) : IDogService
{
    public Task<List<Dog>> GetAvailableDogsAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogImages)
            .Where(d => d.Status == DogStatus.Available || d.Status == DogStatus.Reserved || d.Status == DogStatus.InTreatment)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Dog>> GetAllDogsAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogImages)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<Dog?> GetDogByIdAsync(int id)
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogImages)
            .Include(d => d.MedicalRecords)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task CreateDogAsync(Dog dog)
    {
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
    }
}
