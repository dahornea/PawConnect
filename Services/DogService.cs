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
            .Include(d => d.PreferredFoodType)
            .Where(d => d.Status == DogStatus.Available || d.Status == DogStatus.Reserved)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Dog>> SearchDogsAsync(string? searchTerm, string? breed, int? maxAge, DogSize? size, string? location, DogStatus? status)
    {
        var query = context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .Include(d => d.PreferredFoodType)
            .Where(d => d.Status == DogStatus.Available || d.Status == DogStatus.Reserved)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(d => d.Name.Contains(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(breed))
        {
            query = query.Where(d => d.Breed == breed);
        }

        if (maxAge.HasValue)
        {
            query = query.Where(d => d.Age <= maxAge.Value);
        }

        if (size.HasValue)
        {
            query = query.Where(d => d.Size == size.Value);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(d => d.Location == location);
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        return query
            .OrderBy(d => d.Name)
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
        return GetDogDetailsAsync(id);
    }

    public Task<Dog?> GetDogDetailsAsync(int id)
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .Include(d => d.MedicalRecords)
            .Include(d => d.PreferredFoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task CreateDogAsync(Dog dog)
    {
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
    }
}
