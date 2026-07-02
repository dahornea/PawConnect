using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogImageService(
    ApplicationDbContext context,
    IAuditLogService? auditLogService = null,
    IDogImageStorageService? imageStorageService = null) : IDogImageService
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
        dogImage.ImageUrl = NormalizeImageUrlOrThrow(dogImage.ImageUrl);
        context.DogImages.Add(dogImage);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(DogImage dogImage)
    {
        dogImage.ImageUrl = NormalizeImageUrlOrThrow(dogImage.ImageUrl);
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

    public Task<List<DogImage>> GetImagesForDogAsync(int dogId)
    {
        return context.DogImages
            .Where(i => i.DogId == dogId)
            .OrderByDescending(i => i.IsMainImage)
            .ThenBy(i => i.Id)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddDogImageAsync(int dogId, int shelterId, DogImage image)
    {
        var dog = await EnsureDogCanBeManagedAsync(dogId, shelterId);
        image.ImageUrl = image.ImageUrl?.Trim() ?? string.Empty;
        image.ImageUrl = NormalizeImageUrlOrThrow(image.ImageUrl);

        var normalizedImageUrl = image.ImageUrl.ToUpperInvariant();
        var duplicateExists = await context.DogImages.AnyAsync(i =>
            i.DogId == dogId &&
            i.ImageUrl.Trim().ToUpper() == normalizedImageUrl);

        if (duplicateExists)
        {
            throw new InvalidOperationException("This image has already been added for this dog.");
        }

        if (image.IsMainImage)
        {
            await ClearMainImagesAsync(dogId);
        }

        image.Id = 0;
        image.DogId = dogId;
        image.Dog = null;

        context.DogImages.Add(image);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.DogImageAdded,
            "DogImage",
            image.Id.ToString(),
            $"Image was added for dog {dog.Name}.",
            additionalData: $"DogId={dogId};ShelterId={shelterId}");
    }

    public async Task SetMainImageAsync(int imageId, int shelterId)
    {
        var image = await context.DogImages
            .Include(i => i.Dog)
            .FirstOrDefaultAsync(i => i.Id == imageId);

        if (image?.Dog is null || image.Dog.ShelterId != shelterId)
        {
            throw new InvalidOperationException("Image was not found for your shelter.");
        }

        EnsureDogIsNotAdopted(image.Dog);

        await ClearMainImagesAsync(image.DogId);
        image.IsMainImage = true;
        await context.SaveChangesAsync();
    }

    public async Task DeleteDogImageAsync(int imageId, int shelterId)
    {
        var image = await context.DogImages
            .Include(i => i.Dog)
            .FirstOrDefaultAsync(i => i.Id == imageId);

        if (image?.Dog is null || image.Dog.ShelterId != shelterId)
        {
            throw new InvalidOperationException("Image was not found for your shelter.");
        }

        EnsureDogIsNotAdopted(image.Dog);

        var imagePath = image.ImageUrl;
        context.DogImages.Remove(image);
        await context.SaveChangesAsync();
        if (imageStorageService is not null)
        {
            await imageStorageService.DeleteDogImageAsync(imagePath);
        }

        await LogAsync(
            AuditActions.DogImageDeleted,
            "DogImage",
            imageId.ToString(),
            $"Image was deleted for dog {image.Dog.Name}.",
            additionalData: $"DogId={image.DogId};ShelterId={shelterId}");
    }

    private async Task<Dog> EnsureDogCanBeManagedAsync(int dogId, int shelterId)
    {
        var dog = await context.Dogs.FirstOrDefaultAsync(d => d.Id == dogId && d.ShelterId == shelterId);
        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        EnsureDogIsNotAdopted(dog);
        return dog;
    }

    private static void EnsureDogIsNotAdopted(Dog dog)
    {
        if (dog.Status == DogStatus.Adopted)
        {
            throw new InvalidOperationException("Adopted dogs are read-only for shelter users.");
        }
    }

    private async Task ClearMainImagesAsync(int dogId)
    {
        var currentMainImages = await context.DogImages
            .Where(i => i.DogId == dogId && i.IsMainImage)
            .ToListAsync();

        foreach (var currentMainImage in currentMainImages)
        {
            currentMainImage.IsMainImage = false;
        }
    }

    private static string NormalizeImageUrlOrThrow(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new InvalidOperationException("Image URL is required.");
        }

        if (!DogImageUrlValidator.TryNormalizeImageReference(imageUrl, out var normalizedImageUrl))
        {
            throw new InvalidOperationException(DogImageUrlValidator.ValidationMessage);
        }

        return normalizedImageUrl;
    }

    private Task LogAsync(string action, string entityName, string? entityId, string description, string? additionalData = null)
    {
        return auditLogService?.LogAsync(action, entityName, entityId, description, additionalData: additionalData) ?? Task.CompletedTask;
    }
}
