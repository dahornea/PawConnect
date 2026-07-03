using System.Security.Cryptography;
using System.Text;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogSearchDocumentService : IDogSearchDocumentService
{
    public string BuildDocument(Dog dog)
    {
        var parts = new List<string>
        {
            $"{dog.Name} is a {DogAgeFormatter.Format(dog)} {dog.Size.ToString().ToLowerInvariant()} {DogBreedFormatter.Format(dog)} dog.",
            $"Status: {dog.Status}.",
            $"Location: {dog.Location}."
        };

        if (!string.IsNullOrWhiteSpace(dog.CoatColor))
        {
            parts.Add($"Coat color: {dog.CoatColor.Trim()}.");
        }

        parts.Add($"Cat compatibility: {DogCompatibilityFormatter.FormatCat(dog.CatCompatibility)}.");
        parts.Add($"Dog compatibility: {DogCompatibilityFormatter.FormatDog(dog.DogCompatibility)}.");
        parts.Add($"Children compatibility: {DogCompatibilityFormatter.FormatChildren(dog.ChildrenCompatibility)}.");
        parts.Add($"Activity level: {DogCompatibilityFormatter.FormatActivity(dog.ActivityLevel)}.");
        parts.Add($"Apartment suitability: {DogCompatibilityFormatter.FormatApartment(dog.ApartmentSuitability)}.");
        parts.Add($"Experience needed: {DogCompatibilityFormatter.FormatExperience(dog.ExperienceNeeded)}.");

        if (!string.IsNullOrWhiteSpace(dog.CompatibilityNotes))
        {
            parts.Add($"Compatibility notes: {dog.CompatibilityNotes.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(dog.Shelter?.Name) ||
            !string.IsNullOrWhiteSpace(dog.Shelter?.Neighborhood) ||
            !string.IsNullOrWhiteSpace(dog.Shelter?.City))
        {
            var shelterLocation = string.Join(", ", new[] { dog.Shelter?.Neighborhood, dog.Shelter?.City }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            parts.Add(string.IsNullOrWhiteSpace(shelterLocation)
                ? $"Shelter: {dog.Shelter?.Name}."
                : $"Shelter: {dog.Shelter?.Name} in {shelterLocation}.");
        }

        if (!string.IsNullOrWhiteSpace(dog.Description))
        {
            parts.Add($"Description: {dog.Description.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(dog.BehaviorDescription))
        {
            parts.Add($"Behavior: {dog.BehaviorDescription.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(dog.MedicalStatus))
        {
            parts.Add($"Medical summary: {dog.MedicalStatus.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(dog.PreferredFoodType?.Name))
        {
            parts.Add($"Food preference: {dog.PreferredFoodType.Name.Trim()}.");
        }

        return string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public string ComputeContentHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}
