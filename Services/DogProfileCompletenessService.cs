using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public sealed class DogProfileCompletenessService(ApplicationDbContext context) : IDogProfileCompletenessService
{
    private static readonly IReadOnlyList<string> IdealHomeTerms =
    [
        "home",
        "apartment",
        "yard",
        "house",
        "routine",
        "quiet",
        "active",
        "patient",
        "experienced",
        "family"
    ];

    public DogProfileCompletenessDto CalculateForDog(Dog dog)
    {
        var sections = new List<DogProfileCompletenessSectionDto>
        {
            BuildBasicInformationSection(dog),
            BuildVisualProfileSection(dog),
            BuildDescriptionSection(dog),
            BuildCompatibilitySection(dog),
            BuildHealthAndCareSection(dog),
            BuildAdoptionRequirementsSection(dog),
            BuildShelterLogisticsSection(dog)
        };

        var weightedScore = sections.Sum(section => section.ScorePercent * section.WeightPercent) / 100d;
        var scorePercent = (int)Math.Round(Math.Clamp(weightedScore, 0, 100), MidpointRounding.AwayFromZero);
        var missingItems = sections
            .SelectMany(section => section.MissingItems)
            .OrderByDescending(item => item.IsCritical)
            .ThenBy(item => item.Section)
            .ThenBy(item => item.Label)
            .ToList();
        var recommendations = BuildRecommendations(dog, missingItems);
        var attentionFlags = BuildAttentionFlags(dog, sections, missingItems, scorePercent);

        return new DogProfileCompletenessDto(
            dog.Id,
            dog.Name,
            scorePercent,
            GetLabel(scorePercent),
            sections.Sum(section => section.CompletedItems),
            sections.Sum(section => section.TotalItems),
            sections,
            missingItems.Take(8).ToList(),
            recommendations.Take(5).ToList(),
            attentionFlags.Take(5).ToList(),
            DateTime.UtcNow);
    }

    public async Task<DogProfileCompletenessDto> CalculateForShelterDogAsync(
        int dogId,
        int shelterId,
        CancellationToken cancellationToken = default)
    {
        var dog = await BuildDogQuery()
            .FirstOrDefaultAsync(item => item.Id == dogId && item.ShelterId == shelterId, cancellationToken);

        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        return CalculateForDog(dog);
    }

    public async Task<DogProfileCompletenessDto> CalculateForAdminDogAsync(
        int dogId,
        CancellationToken cancellationToken = default)
    {
        var dog = await BuildDogQuery()
            .FirstOrDefaultAsync(item => item.Id == dogId, cancellationToken);

        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found.");
        }

        return CalculateForDog(dog);
    }

    public IReadOnlyDictionary<int, DogProfileCompletenessDto> CalculateForDogs(IEnumerable<Dog> dogs)
    {
        return dogs.ToDictionary(dog => dog.Id, CalculateForDog);
    }

    public async Task<DogProfileCompletenessSummaryDto> GetShelterCompletenessSummaryAsync(
        int shelterId,
        CancellationToken cancellationToken = default)
    {
        var dogs = await BuildDogQuery()
            .Where(dog => dog.ShelterId == shelterId)
            .ToListAsync(cancellationToken);

        return BuildSummary(dogs.Select(CalculateForDog));
    }

    public async Task<DogProfileCompletenessSummaryDto> GetAdminCompletenessStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var dogs = await BuildDogQuery().ToListAsync(cancellationToken);
        return BuildSummary(dogs.Select(CalculateForDog));
    }

    private IQueryable<Dog> BuildDogQuery()
    {
        return context.Dogs
            .AsNoTracking()
            .Include(dog => dog.Shelter)
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .Include(dog => dog.Images)
            .Include(dog => dog.PreferredFoodType)
            .Include(dog => dog.MedicalRecords);
    }

    private static DogProfileCompletenessSummaryDto BuildSummary(IEnumerable<DogProfileCompletenessDto> results)
    {
        var list = results.ToList();
        return new DogProfileCompletenessSummaryDto(
            list.Count,
            list.Count == 0 ? 0 : Math.Round(list.Average(item => item.ScorePercent), 1),
            list.Count(item => item.Label == "Excellent"),
            list.Count(item => item.Label == "Good"),
            list.Count(item => item.Label == "Needs Work"),
            list.Count(item => item.Label == "Incomplete"),
            list
                .Where(item => item.ScorePercent < 70 || item.MissingItems.Any(missing => missing.IsCritical))
                .OrderBy(item => item.ScorePercent)
                .ThenBy(item => item.DogName)
                .Take(8)
                .ToList());
    }

    private static DogProfileCompletenessSectionDto BuildBasicInformationSection(Dog dog)
    {
        var builder = Section("Basic Information", 20);
        builder.Add(!string.IsNullOrWhiteSpace(dog.Name), "Name", "Add the dog's name.", nameof(Dog.Name), true);
        builder.Add(dog.AgeYears > 0 || dog.AgeMonths > 0 || dog.Age > 0, "Age", "Add the dog's age in years or months.", nameof(Dog.AgeYears), true);
        builder.Add(!string.IsNullOrWhiteSpace(DogBreedFormatter.Format(dog)) && DogBreedFormatter.Format(dog) != "Unknown", "Breed", "Select a breed or clear custom breed description.", nameof(Dog.Breed), true);
        builder.Add(Enum.IsDefined(dog.Size), "Size", "Select a dog size.", nameof(Dog.Size), true);
        builder.Add(Enum.IsDefined(dog.Status), "Adoption status", "Select an adoption status.", nameof(Dog.Status), true);
        builder.Add(!string.IsNullOrWhiteSpace(dog.CoatColor), "Coat color", "Add coat color to improve browsing and Copilot filters.", nameof(Dog.CoatColor));
        return builder.Build();
    }

    private static DogProfileCompletenessSectionDto BuildVisualProfileSection(Dog dog)
    {
        var realImages = dog.Images
            .Where(image => DogImageUrlValidator.IsValidRealDogImageUrl(image.ImageUrl))
            .ToList();
        var hasMainImage = realImages.Any(image => image.IsMainImage) || realImages.Count > 0;
        var builder = Section("Visual Profile", 15);
        builder.Add(hasMainImage, "Main photo", "Add a valid real main photo for listing cards.", "Images", true);
        builder.Add(realImages.Count >= 2, "Gallery photos", "Add at least two real photos so adopters can inspect the dog better.", "Images");
        builder.Add(dog.Images.Count == realImages.Count, "No invalid image records", "Remove placeholder, SVG, or broken-looking image records from this dog.", "Images");
        return builder.Build();
    }

    private static DogProfileCompletenessSectionDto BuildDescriptionSection(Dog dog)
    {
        var builder = Section("Description", 15);
        builder.Add(HasText(dog.Description), "Public description", "Add 2-4 natural sentences about the dog.", nameof(Dog.Description), true);
        builder.Add((dog.Description?.Trim().Length ?? 0) >= 120, "Detailed description", "Add more observable details about routine, personality, and ideal home.", nameof(Dog.Description));
        builder.Add(HasText(dog.BehaviorDescription), "Behavior description", "Describe behavior with people, routines, handling, or new situations.", nameof(Dog.BehaviorDescription), true);
        builder.Add((dog.BehaviorDescription?.Trim().Length ?? 0) >= 70, "Specific behavior evidence", "Add concrete behavior observations instead of generic praise.", nameof(Dog.BehaviorDescription));
        return builder.Build();
    }

    private static DogProfileCompletenessSectionDto BuildCompatibilitySection(Dog dog)
    {
        var builder = Section("Compatibility", 20);
        builder.Add(dog.CatCompatibility != CatCompatibility.Unknown, "Cat compatibility", "Fill in whether the dog has been observed around cats.", nameof(Dog.CatCompatibility), true);
        builder.Add(dog.DogCompatibility != DogCompatibility.Unknown, "Dog compatibility", "Fill in whether the dog is comfortable around other dogs.", nameof(Dog.DogCompatibility), true);
        builder.Add(dog.ChildrenCompatibility != ChildrenCompatibility.Unknown, "Children compatibility", "Fill in child/family compatibility if known.", nameof(Dog.ChildrenCompatibility), true);
        builder.Add(dog.ActivityLevel != DogActivityLevel.Unknown, "Activity level", "Add structured activity level to improve matching.", nameof(Dog.ActivityLevel), true);
        builder.Add(dog.ApartmentSuitability != ApartmentSuitability.Unknown, "Apartment suitability", "Add apartment suitability when known.", nameof(Dog.ApartmentSuitability));
        builder.Add(HasText(dog.CompatibilityNotes), "Compatibility notes", "Add public-safe compatibility notes or introduction cautions.", nameof(Dog.CompatibilityNotes));
        return builder.Build();
    }

    private static DogProfileCompletenessSectionDto BuildHealthAndCareSection(Dog dog)
    {
        var builder = Section("Health & Care", 15);
        builder.Add(HasText(dog.MedicalStatus), "Medical status summary", "Add a short public-safe health or care summary.", nameof(Dog.MedicalStatus), true);
        builder.Add(dog.MedicalRecords.Count > 0, "Medical records", "Add at least one vaccine, checkup, or treatment record.", nameof(Dog.MedicalRecords));
        builder.Add(dog.PreferredFoodTypeId.HasValue || dog.PreferredFoodType is not null, "Preferred food", "Select the dog's preferred food type.", nameof(Dog.PreferredFoodTypeId));
        builder.Add(dog.DailyFoodAmountGrams.HasValue && dog.DailyFoodAmountGrams.Value > 0, "Daily food amount", "Add daily food amount in grams.", nameof(Dog.DailyFoodAmountGrams));
        return builder.Build();
    }

    private static DogProfileCompletenessSectionDto BuildAdoptionRequirementsSection(Dog dog)
    {
        var publicText = string.Join(" ", dog.Description, dog.BehaviorDescription, dog.CompatibilityNotes);
        var builder = Section("Adoption Requirements", 10);
        builder.Add(dog.ExperienceNeeded != DogExperienceNeeded.Unknown, "Experience needed", "Select whether this dog needs a first-time, patient, or experienced adopter.", nameof(Dog.ExperienceNeeded), true);
        builder.Add(HasAny(publicText, IdealHomeTerms), "Ideal home context", "Mention the type of home or routine that would suit the dog.", nameof(Dog.Description));
        builder.Add(HasText(dog.CompatibilityNotes), "Cautions or restrictions", "Add known cautions, slow introductions, or restrictions when relevant.", nameof(Dog.CompatibilityNotes));
        return builder.Build();
    }

    private static DogProfileCompletenessSectionDto BuildShelterLogisticsSection(Dog dog)
    {
        var builder = Section("Shelter & Logistics", 5);
        builder.Add(dog.ShelterId > 0 && dog.Shelter is not null, "Shelter assigned", "Assign this dog to a shelter.", nameof(Dog.Shelter), true);
        builder.Add(!string.IsNullOrWhiteSpace(dog.Location), "Location", "Add public location/neighborhood information.", nameof(Dog.Location), true);
        builder.Add(!string.IsNullOrWhiteSpace(dog.Shelter?.Email) || !string.IsNullOrWhiteSpace(dog.Shelter?.PhoneNumber), "Shelter contact", "Add shelter email or phone number.", nameof(Shelter.Email));
        return builder.Build();
    }

    private static IReadOnlyList<DogProfileCompletenessRecommendationDto> BuildRecommendations(
        Dog dog,
        IReadOnlyList<DogProfileCompletenessMissingItemDto> missingItems)
    {
        var recommendations = new List<DogProfileCompletenessRecommendationDto>();

        if (missingItems.Any(item => item.Section == "Visual Profile"))
        {
            recommendations.Add(new DogProfileCompletenessRecommendationDto(
                "Add real dog photos",
                "A valid main image and at least one gallery image make the profile more trustworthy.",
                "Images"));
        }

        if (missingItems.Any(item => item.Section == "Compatibility"))
        {
            recommendations.Add(new DogProfileCompletenessRecommendationDto(
                "Complete compatibility fields",
                "Cats, dogs, children, activity level, and apartment suitability improve recommendations, saved-search matching, and Copilot explanations.",
                nameof(Dog.CompatibilityNotes)));
        }

        if (missingItems.Any(item => item.Section == "Description"))
        {
            recommendations.Add(new DogProfileCompletenessRecommendationDto(
                "Improve the public description",
                "Add specific shelter observations about routine, energy, behavior, and ideal adopter.",
                nameof(Dog.Description)));
        }

        if (missingItems.Any(item => item.Section == "Health & Care"))
        {
            recommendations.Add(new DogProfileCompletenessRecommendationDto(
                "Add health and care context",
                "Include a public medical summary, food details, or medical records if they are available.",
                nameof(Dog.MedicalStatus)));
        }

        if (dog.ActivityLevel is DogActivityLevel.High && dog.ApartmentSuitability == ApartmentSuitability.Unknown)
        {
            recommendations.Add(new DogProfileCompletenessRecommendationDto(
                "Clarify apartment fit",
                "High-activity dogs need clear notes about exercise and living environment fit.",
                nameof(Dog.ApartmentSuitability)));
        }

        return recommendations;
    }

    private static IReadOnlyList<string> BuildAttentionFlags(
        Dog dog,
        IReadOnlyList<DogProfileCompletenessSectionDto> sections,
        IReadOnlyList<DogProfileCompletenessMissingItemDto> missingItems,
        int scorePercent)
    {
        var flags = new List<string>();

        if (scorePercent < 45)
        {
            flags.Add("Profile is missing several important adopter-facing details.");
        }

        if (missingItems.Any(item => item.IsCritical && item.Section == "Compatibility"))
        {
            flags.Add("Compatibility data is incomplete, which can reduce matching confidence.");
        }

        if (sections.First(section => section.Name == "Visual Profile").ScorePercent < 70)
        {
            flags.Add("Photos need attention before this profile looks polished.");
        }

        if (dog.Status is DogStatus.Available or DogStatus.Reserved && !HasText(dog.Description))
        {
            flags.Add("Public dog is visible but has no description.");
        }

        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string GetLabel(int scorePercent)
    {
        return scorePercent switch
        {
            >= 85 => "Excellent",
            >= 70 => "Good",
            >= 45 => "Needs Work",
            _ => "Incomplete"
        };
    }

    private static bool HasText(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool HasAny(string text, IReadOnlyList<string> terms)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static SectionBuilder Section(string name, int weightPercent)
    {
        return new SectionBuilder(name, weightPercent);
    }

    private sealed class SectionBuilder(string name, int weightPercent)
    {
        private readonly List<DogProfileCompletenessMissingItemDto> _missingItems = [];
        private int _completedItems;
        private int _totalItems;

        public void Add(bool isComplete, string label, string description, string? fieldName = null, bool isCritical = false)
        {
            _totalItems++;
            if (isComplete)
            {
                _completedItems++;
                return;
            }

            _missingItems.Add(new DogProfileCompletenessMissingItemDto(name, label, description, fieldName, isCritical));
        }

        public DogProfileCompletenessSectionDto Build()
        {
            var score = _totalItems == 0
                ? 100
                : (int)Math.Round(_completedItems * 100d / _totalItems, MidpointRounding.AwayFromZero);

            return new DogProfileCompletenessSectionDto(
                name,
                score,
                _completedItems,
                _totalItems,
                weightPercent,
                _missingItems);
        }
    }
}
