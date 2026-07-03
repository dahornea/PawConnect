using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogService(
    ApplicationDbContext context,
    IAuditLogService? auditLogService = null,
    IDogSearchEmbeddingService? dogSearchEmbeddingService = null,
    ILogger<DogService>? logger = null) : IDogService
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
        await ValidateAndNormalizeDogAsync(dog);
        context.Dogs.Update(dog);
        await context.SaveChangesAsync();
        await LogAsync(AuditActions.DogUpdated, "Dog", dog.Id.ToString(), $"Dog {dog.Name} was updated.");
        await RefreshDogSearchEmbeddingBestEffortAsync(dog.Id);
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
        await LogAsync(AuditActions.DogDeleted, "Dog", dog.Id.ToString(), $"Dog {dog.Name} was deleted.");
    }

    public Task<List<Dog>> GetAvailableDogsAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
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
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .Include(d => d.Images)
            .Where(d => d.Status == DogStatus.Adopted)
            .OrderByDescending(d => d.AdoptedAt)
            .ThenBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Dog>> SearchDogsAsync(string? searchTerm, string? breed, int? maxAge, DogSize? size, string? location, DogStatus? status, DogSortOption sortOption = DogSortOption.NameAsc, int? shelterId = null, string? neighborhood = null, string? coatColor = null, CatCompatibility? catCompatibility = null, ChildrenCompatibility? childrenCompatibility = null, DogActivityLevel? activityLevel = null, ApartmentSuitability? apartmentSuitability = null)
    {
        var query = context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
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
            var normalizedBreed = breed.Trim().ToUpper();
            query = query.Where(d =>
                d.Breed.ToUpper().Contains(normalizedBreed) ||
                (d.CustomBreedName != null && d.CustomBreedName.ToUpper().Contains(normalizedBreed)) ||
                (d.DogBreed != null && d.DogBreed.Name.ToUpper().Contains(normalizedBreed)) ||
                (d.SecondaryBreed != null && d.SecondaryBreed.Name.ToUpper().Contains(normalizedBreed)) ||
                (d.IsMixedBreed && d.DogBreed != null && d.SecondaryBreed != null && (d.DogBreed.Name + " " + d.SecondaryBreed.Name + " Mix").ToUpper().Contains(normalizedBreed)) ||
                (d.IsMixedBreed && d.DogBreed != null && (d.DogBreed.Name + " Mix").ToUpper().Contains(normalizedBreed)));
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

        if (shelterId.HasValue)
        {
            query = query.Where(d => d.ShelterId == shelterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(neighborhood))
        {
            var normalizedNeighborhood = neighborhood.Trim().ToUpper();
            query = query.Where(d =>
                d.Shelter != null &&
                d.Shelter.Neighborhood != null &&
                d.Shelter.Neighborhood.Trim().ToUpper() == normalizedNeighborhood);
        }

        var normalizedCoatColor = DogCoatColorOptions.Normalize(coatColor);
        if (!string.IsNullOrWhiteSpace(normalizedCoatColor))
        {
            var upperCoatColor = normalizedCoatColor.ToUpper();
            query = query.Where(d => d.CoatColor != null && d.CoatColor.ToUpper() == upperCoatColor);
        }

        if (catCompatibility is { } catValue && catValue != CatCompatibility.Unknown)
        {
            query = query.Where(d => d.CatCompatibility == catValue);
        }

        if (childrenCompatibility is { } childrenValue && childrenValue != ChildrenCompatibility.Unknown)
        {
            query = query.Where(d => d.ChildrenCompatibility == childrenValue);
        }

        if (activityLevel is { } activityValue && activityValue != DogActivityLevel.Unknown)
        {
            query = query.Where(d => d.ActivityLevel == activityValue);
        }

        if (apartmentSuitability is { } apartmentValue && apartmentValue != ApartmentSuitability.Unknown)
        {
            query = query.Where(d => d.ApartmentSuitability == apartmentValue);
        }

        return ApplyDogSorting(query, sortOption)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Dog>> GetAllDogsAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
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
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .Include(d => d.Images)
            .Include(d => d.MedicalRecords)
            .Include(d => d.PreferredFoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task CreateDogAsync(Dog dog)
    {
        await ValidateAndNormalizeDogAsync(dog);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        await LogAsync(AuditActions.DogCreated, "Dog", dog.Id.ToString(), $"Dog {dog.Name} was created.");
        await RefreshDogSearchEmbeddingBestEffortAsync(dog.Id);
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
        await LogAsync(AuditActions.DogUpdated, "Dog", dog.Id.ToString(), $"Success story information was updated for dog {dog.Name}.");
        await RefreshDogSearchEmbeddingBestEffortAsync(dog.Id);
    }

    public Task<List<Dog>> GetDogsForShelterAsync(int shelterId)
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
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
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .Include(d => d.Images)
            .Include(d => d.PreferredFoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dogId && d.ShelterId == shelterId);
    }

    public async Task CreateDogAsync(Dog dog, int shelterId)
    {
        await ValidateAndNormalizeDogAsync(dog);
        dog.Id = 0;
        dog.ShelterId = shelterId;
        dog.Shelter = null;
        dog.DogBreed = null;
        dog.SecondaryBreed = null;

        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.DogCreated,
            "Dog",
            dog.Id.ToString(),
            $"Dog {dog.Name} was created by a shelter.",
            additionalData: $"ShelterId={shelterId}");
        await RefreshDogSearchEmbeddingBestEffortAsync(dog.Id);
    }

    public async Task UpdateDogAsync(Dog dog, int shelterId, string? changedByUserId = null)
    {
        await ValidateAndNormalizeDogAsync(dog);

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
        existingDog.DogBreedId = dog.DogBreedId;
        existingDog.SecondaryBreedId = dog.SecondaryBreedId;
        existingDog.IsMixedBreed = dog.IsMixedBreed;
        existingDog.CustomBreedName = dog.CustomBreedName;
        existingDog.CoatColor = dog.CoatColor;
        existingDog.CatCompatibility = dog.CatCompatibility;
        existingDog.DogCompatibility = dog.DogCompatibility;
        existingDog.ChildrenCompatibility = dog.ChildrenCompatibility;
        existingDog.ActivityLevel = dog.ActivityLevel;
        existingDog.ExperienceNeeded = dog.ExperienceNeeded;
        existingDog.ApartmentSuitability = dog.ApartmentSuitability;
        existingDog.CompatibilityNotes = string.IsNullOrWhiteSpace(dog.CompatibilityNotes) ? null : dog.CompatibilityNotes.Trim();
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
        await LogAsync(
            AuditActions.DogUpdated,
            "Dog",
            existingDog.Id.ToString(),
            $"Dog {existingDog.Name} was updated by a shelter.",
            userId: changedByUserId,
            additionalData: $"ShelterId={shelterId}");
        await RefreshDogSearchEmbeddingBestEffortAsync(existingDog.Id);

        if (oldStatus != existingDog.Status)
        {
            await LogAsync(
                AuditActions.DogStatusChanged,
                "Dog",
                existingDog.Id.ToString(),
                $"Dog {existingDog.Name} status changed from {oldStatus} to {existingDog.Status}.",
                userId: changedByUserId,
                additionalData: $"ShelterId={shelterId}");
        }
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
        await LogAsync(
            AuditActions.DogDeleted,
            "Dog",
            dog.Id.ToString(),
            $"Dog {dog.Name} was deleted by a shelter.",
            additionalData: $"ShelterId={shelterId}");
    }

    public Task<List<Dog>> GetAllDogsForAdminAsync()
    {
        return context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
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
        await LogAsync(AuditActions.DogDeleted, "Dog", dog.Id.ToString(), $"Dog {dog.Name} was deleted by an admin.");
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
        if (oldStatus != newStatus)
        {
            await LogAsync(
                AuditActions.DogStatusChanged,
                "Dog",
                dogId.ToString(),
                $"Dog status changed from {oldStatus} to {newStatus}.",
                userId: changedByUserId);
        }
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
            DogSortOption.BreedAsc => query.OrderBy(d => d.DogBreed != null ? d.DogBreed.Name : d.CustomBreedName ?? d.Breed).ThenBy(d => d.Name),
            DogSortOption.LocationAsc => query.OrderBy(d => d.Location).ThenBy(d => d.Name),
            DogSortOption.Status => query.OrderBy(d => d.Status).ThenBy(d => d.Name),
            DogSortOption.NewestFirst => query.OrderByDescending(d => d.Id),
            DogSortOption.NearestFirst => query.OrderBy(d => d.Name),
            _ => query.OrderBy(d => d.Name)
        };
    }

    private async Task ValidateAndNormalizeDogAsync(Dog dog)
    {
        NormalizeDogAge(dog);

        if (string.IsNullOrWhiteSpace(dog.Name))
        {
            throw new InvalidOperationException("Dog name is required.");
        }

        await NormalizeDogBreedAsync(dog);

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

        if (dog.DailyFoodAmountGrams.HasValue && dog.DailyFoodAmountGrams.Value < 0)
        {
            throw new InvalidOperationException("Daily food amount must be zero or greater when provided.");
        }

        if (!string.IsNullOrWhiteSpace(dog.CompatibilityNotes) && dog.CompatibilityNotes.Length > 1000)
        {
            throw new InvalidOperationException("Compatibility notes must be 1000 characters or fewer.");
        }

        dog.Name = dog.Name.Trim();
        dog.Breed = dog.Breed.Trim();
        dog.CustomBreedName = string.IsNullOrWhiteSpace(dog.CustomBreedName) ? null : dog.CustomBreedName.Trim();
        dog.CoatColor = DogCoatColorOptions.Normalize(dog.CoatColor);
        dog.CompatibilityNotes = string.IsNullOrWhiteSpace(dog.CompatibilityNotes) ? null : dog.CompatibilityNotes.Trim();
        dog.Location = dog.Location.Trim();
        dog.Age = dog.AgeYears;
    }

    private async Task NormalizeDogBreedAsync(Dog dog)
    {
        var breeds = await context.DogBreeds.AsNoTracking().ToListAsync();

        if (!dog.DogBreedId.HasValue && string.IsNullOrWhiteSpace(dog.CustomBreedName) && !string.IsNullOrWhiteSpace(dog.Breed))
        {
            var parsed = DogBreedFormatter.Parse(dog.Breed, breeds);
            dog.DogBreedId = parsed.DogBreedId;
            dog.SecondaryBreedId = parsed.SecondaryBreedId;
            dog.IsMixedBreed = parsed.IsMixedBreed;
            dog.CustomBreedName = parsed.CustomBreedName;
            dog.Breed = parsed.DisplayName;
        }

        var selectedBreed = dog.DogBreedId.HasValue
            ? breeds.FirstOrDefault(breed => breed.Id == dog.DogBreedId.Value)
            : null;
        var selectedSecondaryBreed = dog.SecondaryBreedId.HasValue
            ? breeds.FirstOrDefault(breed => breed.Id == dog.SecondaryBreedId.Value)
            : null;

        if (dog.DogBreedId.HasValue && selectedBreed is null)
        {
            throw new InvalidOperationException("Selected breed was not found.");
        }

        if (dog.SecondaryBreedId.HasValue && selectedSecondaryBreed is null)
        {
            throw new InvalidOperationException("Selected secondary breed was not found.");
        }

        dog.CustomBreedName = string.IsNullOrWhiteSpace(dog.CustomBreedName) ? null : dog.CustomBreedName.Trim();
        if (!dog.DogBreedId.HasValue && string.IsNullOrWhiteSpace(dog.CustomBreedName))
        {
            var unknownBreed = breeds.FirstOrDefault(breed => breed.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase));
            dog.DogBreedId = unknownBreed?.Id;
            selectedBreed = unknownBreed;
        }

        if (!dog.IsMixedBreed ||
            IsSpecialBreed(selectedBreed) ||
            IsSpecialBreed(selectedSecondaryBreed) ||
            selectedBreed?.Id == selectedSecondaryBreed?.Id)
        {
            dog.SecondaryBreedId = null;
            selectedSecondaryBreed = null;
        }

        dog.DogBreed = null;
        dog.SecondaryBreed = null;
        dog.Breed = DogBreedFormatter.Format(selectedBreed?.Name, selectedSecondaryBreed?.Name, dog.IsMixedBreed, dog.CustomBreedName, dog.Breed);
    }

    private static bool IsSpecialBreed(DogBreed? breed)
    {
        return breed is not null &&
            (breed.Name.Equals("Mixed Breed", StringComparison.OrdinalIgnoreCase) ||
             breed.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase));
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

    private Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        string? userId = null,
        string? additionalData = null)
    {
        return auditLogService?.LogAsync(action, entityName, entityId, description, userId: userId, additionalData: additionalData) ?? Task.CompletedTask;
    }

    private async Task RefreshDogSearchEmbeddingBestEffortAsync(int dogId)
    {
        if (dogSearchEmbeddingService is null)
        {
            return;
        }

        try
        {
            await dogSearchEmbeddingService.RefreshDogEmbeddingAsync(dogId);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Dog search embedding refresh failed after dog change for DogId {DogId}.", dogId);
        }
    }
}
