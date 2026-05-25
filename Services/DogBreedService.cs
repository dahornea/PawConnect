using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogBreedService(ApplicationDbContext context) : IDogBreedService
{
    public Task<List<DogBreed>> GetActiveBreedsAsync()
    {
        return context.DogBreeds
            .Where(breed => breed.IsActive)
            .OrderBy(breed => breed.Name)
            .AsNoTracking()
            .ToListAsync();
    }
}
