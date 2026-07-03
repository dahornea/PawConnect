using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogRecommendationService(
    ApplicationDbContext context,
    IOptions<OpenAiSettings> openAiOptions,
    IOpenAiRecommendationClient openAiClient,
    ILogger<DogRecommendationService> logger) : IDogRecommendationService
{
    private const int OpenAiCandidateLimit = 10;

    public async Task<IReadOnlyList<DogRecommendationResult>> GetRecommendationsForAdopterAsync(string adopterUserId, int count = 10)
    {
        var safeCount = Math.Max(1, count);
        var candidateCount = Math.Max(safeCount, OpenAiCandidateLimit);
        var ruleBased = await GetRuleBasedRecommendationsAsync(adopterUserId, candidateCount);

        if (ruleBased.Count == 0)
        {
            return [];
        }

        var settings = openAiOptions.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            return ruleBased.Take(safeCount).ToList();
        }

        try
        {
            return await GetOpenAiEnhancedRecommendationsAsync(
                adopterUserId,
                ruleBased.Take(OpenAiCandidateLimit).ToList(),
                safeCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenAI recommendation enhancement failed unexpectedly. Using rule-based recommendations.");
            return ruleBased.Take(safeCount).ToList();
        }
    }

    public async Task<IReadOnlyList<DogRecommendationResult>> GetRuleBasedRecommendationsAsync(string adopterUserId, int count = 10)
    {
        if (string.IsNullOrWhiteSpace(adopterUserId))
        {
            return [];
        }

        var profile = await context.AdopterProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.ApplicationUserId == adopterUserId);

        if (profile is null)
        {
            return [];
        }

        var dogs = await context.Dogs
            .Include(dog => dog.Shelter)
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .Include(dog => dog.Images)
            .Include(dog => dog.PreferredFoodType)
            .Where(dog => dog.Status == DogStatus.Available || dog.Status == DogStatus.Reserved)
            .AsNoTracking()
            .ToListAsync();

        if (dogs.Count == 0)
        {
            return [];
        }

        var favoriteDogs = await context.FavoriteDogs
            .Include(favorite => favorite.Dog)
            .ThenInclude(dog => dog!.DogBreed)
            .Include(favorite => favorite.Dog)
            .ThenInclude(dog => dog!.SecondaryBreed)
            .Where(favorite => favorite.AdopterId == adopterUserId && favorite.Dog != null)
            .AsNoTracking()
            .ToListAsync();

        var recentDogs = await context.RecentlyViewedDogs
            .Include(view => view.Dog)
            .ThenInclude(dog => dog!.DogBreed)
            .Include(view => view.Dog)
            .ThenInclude(dog => dog!.SecondaryBreed)
            .Where(view => view.AdopterId == adopterUserId && view.Dog != null)
            .AsNoTracking()
            .ToListAsync();
        var favoriteTraits = favoriteDogs.Select(favorite => new DogTrait(DogBreedFormatter.Format(favorite.Dog), favorite.Dog!.Size)).ToList();
        var recentTraits = recentDogs.Select(view => new DogTrait(DogBreedFormatter.Format(view.Dog), view.Dog!.Size)).ToList();

        return dogs
            .Select(dog => ScoreDog(profile, dog, favoriteTraits, recentTraits))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Dog.Name)
            .Take(Math.Max(1, count))
            .ToList();
    }

    public async Task<IReadOnlyList<DogRecommendationResult>> GetOpenAiEnhancedRecommendationsAsync(
        string adopterUserId,
        IReadOnlyList<DogRecommendationResult> candidates,
        int count = 10)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var settings = openAiOptions.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            return candidates.Take(Math.Max(1, count)).ToList();
        }

        var profile = await context.AdopterProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.ApplicationUserId == adopterUserId);

        if (profile is null)
        {
            return candidates.Take(Math.Max(1, count)).ToList();
        }

        var request = BuildOpenAiRequest(profile, candidates.Take(OpenAiCandidateLimit).ToList());
        var response = await openAiClient.GetEnhancedRecommendationsAsync(request);
        if (!response.Success)
        {
            logger.LogWarning("OpenAI recommendation response was unusable. Falling back to rule-based recommendations.");
            return candidates.Take(Math.Max(1, count)).ToList();
        }

        var candidatesById = candidates.ToDictionary(candidate => candidate.DogId);
        var enhanced = new List<DogRecommendationResult>();
        var usedDogIds = new HashSet<int>();

        foreach (var item in response.Recommendations.OrderBy(item => item.Rank))
        {
            if (!candidatesById.TryGetValue(item.DogId, out var candidate) || !usedDogIds.Add(item.DogId))
            {
                continue;
            }

            enhanced.Add(candidate with
            {
                MatchLevel = NormalizeMatchLevel(item.MatchLabel, candidate.MatchLevel),
                Reasons = item.Reasons.Count == 0 ? candidate.Reasons : item.Reasons.Take(3).ToList(),
                ShortSummary = string.IsNullOrWhiteSpace(item.ShortSummary) ? candidate.ShortSummary : item.ShortSummary,
                ReasonCategories = item.Categories is { Count: > 0 }
                    ? item.Categories
                        .Where(category => !string.IsNullOrWhiteSpace(category.Category) && !string.IsNullOrWhiteSpace(category.Text))
                        .Select((category, index) => new DogRecommendationReason(
                            NormalizeReasonCategory(category.Category),
                            category.Text.Trim(),
                            100 - index))
                        .Take(4)
                        .ToList()
                    : candidate.ReasonCategories,
                UsedAiEnhancement = true
            });
        }

        foreach (var candidate in candidates)
        {
            if (usedDogIds.Add(candidate.DogId))
            {
                enhanced.Add(candidate);
            }
        }

        return enhanced.Take(Math.Max(1, count)).ToList();
    }

    private static DogRecommendationResult ScoreDog(
        AdopterProfile profile,
        Dog dog,
        IReadOnlyList<DogTrait> favoriteTraits,
        IReadOnlyList<DogTrait> recentTraits)
    {
        var score = 10d;
        var reasons = new List<DogRecommendationReason>();
        var profilePreferenceText = $"{profile.ExperienceWithDogs} {profile.AdditionalNotes}".ToLowerInvariant();

        var profileCity = Normalize(profile.City);
        var shelterCity = Normalize(dog.Shelter?.City);
        if (!string.IsNullOrWhiteSpace(profileCity) && profileCity == shelterCity)
        {
            AddReason(reasons, "Location fit", "Located in the same city as your profile.", 22);
            score += 22;
        }

        if (profile.HousingType == HousingType.Apartment)
        {
            if (dog.Size == DogSize.Small)
            {
                AddReason(reasons, "Home fit", "Small size may suit apartment living.", 20);
                score += 20;
            }
            else if (dog.Size == DogSize.Medium)
            {
                AddReason(reasons, "Home fit", "Medium size may suit apartment living.", 14);
                score += 14;
            }
            else
            {
                score -= 6;
            }

            if (dog.ApartmentSuitability == ApartmentSuitability.Suitable)
            {
                AddReason(reasons, "Home fit", "Shelter marked this dog as suitable for apartment living.", 14);
                score += 14;
            }
            else if (dog.ApartmentSuitability == ApartmentSuitability.MaybeWithRoutine)
            {
                AddReason(reasons, "Home fit", "Shelter marked this dog as possible for an apartment with routine.", 8);
                score += 8;
            }
            else if (dog.ApartmentSuitability == ApartmentSuitability.NotRecommended)
            {
                score -= 12;
            }

            if (dog.ActivityLevel == DogActivityLevel.Low)
            {
                AddReason(reasons, "Home fit", "Lower activity level may fit apartment routines.", 7);
                score += 7;
            }
            else if (dog.ActivityLevel == DogActivityLevel.High)
            {
                score -= 7;
            }
        }

        if (profile.HousingType == HousingType.House || profile.HasYard)
        {
            if (dog.Size is DogSize.Medium or DogSize.Large)
            {
                AddReason(reasons, "Home fit", "Size may suit a home with more space.", 18);
                score += 18;
            }
            else if (dog.Size == DogSize.Small)
            {
                AddReason(reasons, "Home fit", "Smaller size can still feel comfortable at home.", 8);
                score += 8;
            }

            if (dog.ActivityLevel == DogActivityLevel.High)
            {
                AddReason(reasons, "Home fit", "Higher activity level can suit a home with more outdoor space.", 8);
                score += 8;
            }
            else if (dog.ActivityLevel == DogActivityLevel.Medium)
            {
                AddReason(reasons, "Home fit", "Medium activity level can suit regular home routines.", 5);
                score += 5;
            }
        }

        var dogText = $"{dog.Description} {dog.BehaviorDescription}".ToLowerInvariant();
        if (profile.HasChildren)
        {
            if (dog.ChildrenCompatibility == ChildrenCompatibility.Yes)
            {
                AddReason(reasons, "Behavior fit", "Shelter marked this dog as compatible with children.", 16);
                score += 16;
            }
            else if (dog.ChildrenCompatibility == ChildrenCompatibility.OlderChildrenOnly)
            {
                AddReason(reasons, "Behavior fit", "Shelter recommends older children only.", 7);
                score += 7;
            }
            else if (dog.ChildrenCompatibility == ChildrenCompatibility.No)
            {
                score -= 16;
            }
        }

        if (profile.HasOtherPets)
        {
            if (dog.CatCompatibility == CatCompatibility.Yes || dog.DogCompatibility == DogCompatibility.Yes)
            {
                AddReason(reasons, "Behavior fit", "Shelter compatibility notes mention other pets.", 14);
                score += 14;
            }
            else if (dog.CatCompatibility == CatCompatibility.SlowIntroductions ||
                     dog.DogCompatibility is DogCompatibility.CalmDogsOnly or DogCompatibility.SlowIntroductions)
            {
                AddReason(reasons, "Behavior fit", "Shelter recommends careful pet introductions.", 7);
                score += 7;
            }
            else if (dog.CatCompatibility == CatCompatibility.No || dog.DogCompatibility is DogCompatibility.No or DogCompatibility.OnlyDog)
            {
                score -= 14;
            }
        }

        var beginnerProfile = string.IsNullOrWhiteSpace(profile.ExperienceWithDogs) ||
            ContainsAny(profilePreferenceText, "beginner", "first time", "first-time", "new adopter");
        if (beginnerProfile)
        {
            if (dog.ExperienceNeeded == DogExperienceNeeded.Beginner)
            {
                AddReason(reasons, "Experience fit", "Shelter marked this dog as suitable for a beginner adopter.", 10);
                score += 10;
            }
            else if (dog.ExperienceNeeded == DogExperienceNeeded.SomeExperience)
            {
                AddReason(reasons, "Experience fit", "Some experience may be helpful for this dog.", 4);
                score += 4;
            }
            else if (dog.ExperienceNeeded == DogExperienceNeeded.Experienced)
            {
                score -= 8;
            }
        }
        else if (dog.ExperienceNeeded == DogExperienceNeeded.Experienced)
        {
            AddReason(reasons, "Experience fit", "Your experience may fit a dog that needs a more confident adopter.", 8);
            score += 8;
        }

        if (profile.HasChildren && ContainsAny(dogText, "child", "children", "kids", "family", "gentle", "calm"))
        {
            AddReason(reasons, "Behavior fit", "Behavior notes suggest a family-friendly temperament.", 16);
            score += 16;
        }
        else if (profile.HasChildren && ContainsAny(dogText, "active", "high energy", "jumpy", "training"))
        {
            score -= 4;
        }

        if (profile.HasOtherPets && ContainsAny(dogText, "social", "sociable", "friendly", "other dogs", "pets"))
        {
            AddReason(reasons, "Behavior fit", "Social and friendly behavior profile.", 14);
            score += 14;
        }

        if (!string.IsNullOrWhiteSpace(profile.ExperienceWithDogs) &&
            ContainsAny(dogText, "active", "training", "energetic", "experienced", "high energy"))
        {
            AddReason(reasons, "Experience fit", "Good fit for someone with dog experience.", 12);
            score += 12;
        }
        else if (string.IsNullOrWhiteSpace(profile.ExperienceWithDogs) &&
                 ContainsAny(dogText, "calm", "easy", "gentle", "quiet"))
        {
            AddReason(reasons, "Experience fit", "Calmer notes can be approachable for a newer adopter.", 8);
            score += 8;
        }

        if (ContainsAny(profilePreferenceText, "active", "run", "hike", "walk") &&
            ContainsAny(dogText, "active", "energetic", "playful", "walk"))
        {
            AddReason(reasons, "Preferences fit", "Activity level aligns with your profile notes.", 9);
            score += 9;
        }

        if (ContainsAny(profilePreferenceText, "calm", "quiet", "relaxed") &&
            ContainsAny(dogText, "calm", "quiet", "gentle", "relaxed"))
        {
            AddReason(reasons, "Preferences fit", "Temperament notes align with your preferences.", 9);
            score += 9;
        }

        if (dog.AgeYears <= 1 && ContainsAny(profilePreferenceText, "puppy", "young"))
        {
            AddReason(reasons, "Preferences fit", "Young age aligns with your profile notes.", 7);
            score += 7;
        }

        if (dog.AgeYears >= 7 && ContainsAny(profilePreferenceText, "senior", "older", "calm"))
        {
            AddReason(reasons, "Preferences fit", "Older profile aligns with calmer preference notes.", 7);
            score += 7;
        }

        var preferenceTraits = favoriteTraits.Concat(recentTraits).ToList();
        var dogBreed = DogBreedFormatter.Format(dog);
        var breedMatches = preferenceTraits.Count(trait => string.Equals(trait.Breed, dogBreed, StringComparison.OrdinalIgnoreCase));
        var sizeMatches = preferenceTraits.Count(trait => trait.Size == dog.Size);
        if (breedMatches >= 2)
        {
            AddReason(reasons, "Preferences fit", "Similar to dogs you recently viewed or saved.", 8);
            score += 8;
        }
        else if (breedMatches == 1)
        {
            score += 4;
        }

        if (breedMatches == 0 && sizeMatches >= 2)
        {
            AddReason(reasons, "Preferences fit", "Similar in size to dogs you recently viewed or saved.", 5);
            score += 5;
        }
        else if (sizeMatches == 1)
        {
            score += 2;
        }

        if (dog.Status == DogStatus.Available)
        {
            score += 5;
        }

        if (reasons.Count == 0)
        {
            AddReason(reasons, "Match fit", "Public profile has enough details to consider.", 4);
        }

        var topReasons = reasons
            .GroupBy(reason => reason.Category)
            .Select(group => group.OrderByDescending(reason => reason.Weight).First())
            .OrderByDescending(reason => reason.Weight)
            .Take(4)
            .ToList();
        var percentage = NormalizeMatchPercentage(score);

        return new DogRecommendationResult(
            dog.Id,
            dog,
            score,
            GetMatchLevel(percentage),
            topReasons.Select(reason => reason.Text).ToList(),
            MatchPercentage: percentage,
            ShortSummary: BuildShortSummary(dog, topReasons),
            ReasonCategories: topReasons);
    }

    private static RecommendationOpenAiRequest BuildOpenAiRequest(
        AdopterProfile profile,
        IReadOnlyList<DogRecommendationResult> candidates)
    {
        return new RecommendationOpenAiRequest(
            new RecommendationAdopterProfileInput(
                SafeTrim(profile.City),
                profile.HousingType.ToString(),
                profile.HasYard,
                profile.HasOtherPets,
                profile.HasChildren,
                SafeTrim(profile.ExperienceWithDogs, 300)),
            candidates.Select(candidate => new RecommendationDogCandidateInput(
                    candidate.DogId,
                    DogBreedFormatter.Format(candidate.Dog),
                    SafeTrim(candidate.Dog.CoatColor),
                    DogAgeFormatter.Format(candidate.Dog),
                    candidate.Dog.Size.ToString(),
                    candidate.Dog.Status.ToString(),
                    DogCompatibilityFormatter.FormatCat(candidate.Dog.CatCompatibility),
                    DogCompatibilityFormatter.FormatDog(candidate.Dog.DogCompatibility),
                    DogCompatibilityFormatter.FormatChildren(candidate.Dog.ChildrenCompatibility),
                    DogCompatibilityFormatter.FormatActivity(candidate.Dog.ActivityLevel),
                    DogCompatibilityFormatter.FormatExperience(candidate.Dog.ExperienceNeeded),
                    DogCompatibilityFormatter.FormatApartment(candidate.Dog.ApartmentSuitability),
                    candidate.MatchPercentage,
                    candidate.MatchLevel,
                    candidate.ShortSummary,
                    SafeTrim(candidate.Dog.Description, 350),
                    SafeTrim(candidate.Dog.BehaviorDescription, 350),
                    SafeTrim(candidate.Dog.Shelter?.City),
                    candidate.DistanceKm,
                    candidate.Reasons,
                    (candidate.ReasonCategories ?? [])
                        .Select(reason => new RecommendationReasonInput(reason.Category, reason.Text))
                        .ToList()))
                .ToList());
    }

    private static int NormalizeMatchPercentage(double score)
    {
        return Math.Clamp((int)Math.Round(50 + (score * 0.42)), 52, 96);
    }

    private static string GetMatchLevel(int matchPercentage)
    {
        return matchPercentage switch
        {
            >= 84 => "Excellent match",
            >= 68 => "Good match",
            _ => "Possible match"
        };
    }

    private static string NormalizeMatchLevel(string? proposed, string fallback)
    {
        return proposed?.Trim() switch
        {
            "Excellent match" => "Excellent match",
            "Good match" => "Good match",
            "Possible match" => "Possible match",
            _ => fallback
        };
    }

    private static string NormalizeReasonCategory(string? category)
    {
        return category?.Trim() switch
        {
            "Home fit" => "Home fit",
            "Experience fit" => "Experience fit",
            "Location fit" => "Location fit",
            "Behavior fit" => "Behavior fit",
            "Preferences fit" => "Preferences fit",
            _ => "Match fit"
        };
    }

    private static void AddReason(List<DogRecommendationReason> reasons, string category, string text, int weight)
    {
        if (reasons.Any(reason =>
                string.Equals(reason.Category, category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reason.Text, text, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        reasons.Add(new DogRecommendationReason(category, text, weight));
    }

    private static string BuildShortSummary(Dog dog, IReadOnlyList<DogRecommendationReason> reasons)
    {
        if (reasons.Count == 0)
        {
            return $"{dog.Name} is available to consider based on public profile details.";
        }

        var strongest = reasons[0];
        return strongest.Category switch
        {
            "Home fit" when strongest.Text.Contains("apartment", StringComparison.OrdinalIgnoreCase)
                => $"{dog.Name} could be a good match because their size may suit apartment living.",
            "Home fit"
                => $"{dog.Name} could be a good fit for your home because their size and profile match your living situation.",
            "Experience fit"
                => $"{dog.Name} may be a good match because their profile could work well for your experience level.",
            "Location fit"
                => $"{dog.Name} could be easier to meet because the shelter is close to your profile location.",
            "Behavior fit"
                => $"{GetPossessiveName(dog.Name)} behavior notes suggest traits that could fit your household.",
            "Preferences fit"
                => $"{dog.Name} has traits that line up with dogs you have recently viewed or saved.",
            _ => $"{dog.Name} could be a good match based on the public profile details."
        };
    }

    private static string GetPossessiveName(string dogName)
    {
        return dogName.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? $"{dogName}'"
            : $"{dogName}'s";
    }

    private static string? SafeTrim(string? value, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record DogTrait(string Breed, DogSize Size);
}
