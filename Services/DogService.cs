using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogService(ApplicationDbContext context) : IDogService
{
    public Task<List<Dog>> GetAllAsync()
    {
        return GetAllDogsAsync();
    }

    public Task<Dog?> GetByIdAsync(int id)
    {
        return GetDogByIdAsync(id);
    }

    public Task CreateAsync(Dog dog)
    {
        return CreateDogAsync(dog);
    }

    public async Task UpdateAsync(Dog dog)
    {
        context.Dogs.Update(dog);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var dog = await context.Dogs.FindAsync(id);
        if (dog is null)
        {
            return;
        }

        context.Dogs.Remove(dog);
        await context.SaveChangesAsync();
    }

    public Task<List<Dog>> GetAvailableDogsAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .Where(d => d.Status == DogStatus.Available || d.Status == DogStatus.Reserved || d.Status == DogStatus.InTreatment)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Dog>> GetAllDogsAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<Dog?> GetDogByIdAsync(int id)
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
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
