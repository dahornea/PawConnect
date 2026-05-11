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
        NormalizeDogAge(dog);
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

    public Task<List<Dog>> GetAdoptedDogsAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .Where(d => d.Status == DogStatus.Adopted)
            .OrderByDescending(d => d.AdoptedAt)
            .ThenBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Dog>> SearchDogsAsync(string? searchTerm, string? breed, int? maxAge, DogSize? size, string? location, DogStatus? status, DogSortOption sortOption = DogSortOption.NameAsc)
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
            query = query.Where(d => d.AgeYears <= maxAge.Value);
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

        return ApplyDogSorting(query, sortOption)
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
        NormalizeDogAge(dog);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
    }

    public async Task UpdateSuccessStoryAsync(int dogId, int shelterId, string? successStoryText, DateTime? adoptedAt)
    {
        if (!string.IsNullOrWhiteSpace(successStoryText) && successStoryText.Length > 2000)
        {
            throw new InvalidOperationException("Success story text must be 2000 characters or fewer.");
        }

        var dog = await context.Dogs.FirstOrDefaultAsync(d => d.Id == dogId && d.ShelterId == shelterId);
        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        dog.SuccessStoryText = string.IsNullOrWhiteSpace(successStoryText) ? null : successStoryText.Trim();
        dog.AdoptedAt = adoptedAt;

        await context.SaveChangesAsync();
    }

    public Task<List<Dog>> GetDogsForShelterAsync(int shelterId)
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .Include(d => d.PreferredFoodType)
            .Where(d => d.ShelterId == shelterId)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<Dog?> GetDogForShelterAsync(int dogId, int shelterId)
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .Include(d => d.PreferredFoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dogId && d.ShelterId == shelterId);
    }

    public async Task CreateDogAsync(Dog dog, int shelterId)
    {
        ValidateDog(dog);
        dog.Id = 0;
        dog.ShelterId = shelterId;
        dog.Shelter = null;

        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
    }

    public async Task UpdateDogAsync(Dog dog, int shelterId, string? changedByUserId = null)
    {
        ValidateDog(dog);

        var existingDog = await context.Dogs.FirstOrDefaultAsync(d => d.Id == dog.Id && d.ShelterId == shelterId);
        if (existingDog is null)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        var oldStatus = existingDog.Status;
        if (oldStatus == DogStatus.Adopted)
        {
            throw new InvalidOperationException("Adopted dogs are read-only for shelter users. Contact an admin if this was a mistake.");
        }

        existingDog.Name = dog.Name.Trim();
        existingDog.Breed = dog.Breed.Trim();
        existingDog.AgeYears = dog.AgeYears;
        existingDog.AgeMonths = dog.AgeMonths;
        existingDog.Age = dog.AgeYears;
        existingDog.Size = dog.Size;
        existingDog.Location = dog.Location.Trim();
        existingDog.Description = string.IsNullOrWhiteSpace(dog.Description) ? null : dog.Description.Trim();
        existingDog.BehaviorDescription = string.IsNullOrWhiteSpace(dog.BehaviorDescription) ? null : dog.BehaviorDescription.Trim();
        existingDog.MedicalStatus = string.IsNullOrWhiteSpace(dog.MedicalStatus) ? null : dog.MedicalStatus.Trim();
        existingDog.Status = dog.Status;
        existingDog.PreferredFoodTypeId = dog.PreferredFoodTypeId;
        existingDog.DailyFoodAmountGrams = dog.DailyFoodAmountGrams;

        AddStatusHistoryIfChanged(existingDog.Id, oldStatus, existingDog.Status, changedByUserId, "Status updated by shelter.");

        await context.SaveChangesAsync();
    }

    public async Task DeleteDogAsync(int dogId, int shelterId)
    {
        var dog = await context.Dogs
            .Include(d => d.AdoptionRequests)
            .Include(d => d.FavoriteDogs)
            .FirstOrDefaultAsync(d => d.Id == dogId && d.ShelterId == shelterId);

        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        if (dog.AdoptionRequests.Count > 0)
        {
            throw new InvalidOperationException("This dog cannot be deleted because it has adoption request history. To remove it from public listings, change its status instead.");
        }

        await RemoveDogReferencesThatDoNotNeedHistoryAsync(dog.Id);

        context.Dogs.Remove(dog);
        await context.SaveChangesAsync();
    }

    public Task<List<Dog>> GetAllDogsForAdminAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .Include(d => d.PreferredFoodType)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task DeleteDogForAdminAsync(int dogId)
    {
        var dog = await context.Dogs
            .Include(d => d.AdoptionRequests)
            .Include(d => d.FavoriteDogs)
            .FirstOrDefaultAsync(d => d.Id == dogId);

        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found.");
        }

        if (dog.AdoptionRequests.Count > 0)
        {
            throw new InvalidOperationException("This dog cannot be deleted because it has adoption request history. To remove it from public listings, change its status instead.");
        }

        await RemoveDogReferencesThatDoNotNeedHistoryAsync(dog.Id);

        context.Dogs.Remove(dog);
        await context.SaveChangesAsync();
    }

    public Task<List<DogStatusHistory>> GetStatusHistoryForDogAsync(int dogId)
    {
        return context.DogStatusHistories
            .Include(h => h.ChangedByUser)
            .Where(h => h.DogId == dogId)
            .OrderByDescending(h => h.ChangedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<DogStatusHistory>> GetStatusHistoryForShelterDogAsync(int dogId, int shelterId)
    {
        var dogExistsForShelter = await context.Dogs.AnyAsync(d => d.Id == dogId && d.ShelterId == shelterId);
        if (!dogExistsForShelter)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        return await GetStatusHistoryForDogAsync(dogId);
    }

    public async Task AddStatusHistoryAsync(int dogId, DogStatus oldStatus, DogStatus newStatus, string? changedByUserId, string? notes = null)
    {
        AddStatusHistoryIfChanged(dogId, oldStatus, newStatus, changedByUserId, notes);
        await context.SaveChangesAsync();
    }

    private void AddStatusHistoryIfChanged(int dogId, DogStatus oldStatus, DogStatus newStatus, string? changedByUserId, string? notes = null)
    {
        if (oldStatus == newStatus)
        {
            return;
        }

        context.DogStatusHistories.Add(new DogStatusHistory
        {
            DogId = dogId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = changedByUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
    }

    private static IQueryable<Dog> ApplyDogSorting(IQueryable<Dog> query, DogSortOption sortOption)
    {
        return sortOption switch
        {
            DogSortOption.NameDesc => query.OrderByDescending(d => d.Name),
            DogSortOption.AgeAsc => query.OrderBy(d => d.AgeYears).ThenBy(d => d.AgeMonths).ThenBy(d => d.Name),
            DogSortOption.AgeDesc => query.OrderByDescending(d => d.AgeYears).ThenByDescending(d => d.AgeMonths).ThenBy(d => d.Name),
            DogSortOption.BreedAsc => query.OrderBy(d => d.Breed).ThenBy(d => d.Name),
            DogSortOption.LocationAsc => query.OrderBy(d => d.Location).ThenBy(d => d.Name),
            DogSortOption.Status => query.OrderBy(d => d.Status).ThenBy(d => d.Name),
            DogSortOption.NewestFirst => query.OrderByDescending(d => d.Id),
            _ => query.OrderBy(d => d.Name)
        };
    }

    private static void ValidateDog(Dog dog)
    {
        NormalizeDogAge(dog);

        if (string.IsNullOrWhiteSpace(dog.Name))
        {
            throw new InvalidOperationException("Dog name is required.");
        }

        if (string.IsNullOrWhiteSpace(dog.Breed))
        {
            throw new InvalidOperationException("Breed is required.");
        }

        if (dog.AgeYears is < 0 or > 30)
        {
            throw new InvalidOperationException("Age in years must be between 0 and 30.");
        }

        if (dog.AgeMonths is < 0 or > 11)
        {
            throw new InvalidOperationException("Age in months must be between 0 and 11.");
        }

        if (dog.AgeYears == 0 && dog.AgeMonths == 0)
        {
            throw new InvalidOperationException("Please enter the dog's age in years or months.");
        }

        if (string.IsNullOrWhiteSpace(dog.Location))
        {
            throw new InvalidOperationException("Location is required.");
        }

        if (dog.DailyFoodAmountGrams.HasValue && dog.DailyFoodAmountGrams.Value <= 0)
        {
            throw new InvalidOperationException("Daily food amount must be positive when provided.");
        }

        dog.Name = dog.Name.Trim();
        dog.Breed = dog.Breed.Trim();
        dog.Location = dog.Location.Trim();
        dog.Age = dog.AgeYears;
    }

    private static void NormalizeDogAge(Dog dog)
    {
        if (dog.AgeYears == 0 && dog.AgeMonths == 0 && dog.Age > 0)
        {
            dog.AgeYears = dog.Age;
        }

        dog.Age = dog.AgeYears;
    }

    private async Task RemoveDogReferencesThatDoNotNeedHistoryAsync(int dogId)
    {
        var favorites = await context.FavoriteDogs
            .Where(f => f.DogId == dogId)
            .ToListAsync();

        var recentViews = await context.RecentlyViewedDogs
            .Where(v => v.DogId == dogId)
            .ToListAsync();

        context.FavoriteDogs.RemoveRange(favorites);
        context.RecentlyViewedDogs.RemoveRange(recentViews);
    }
}
