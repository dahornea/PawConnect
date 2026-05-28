using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class AdoptionCopilotToolService(
    ApplicationDbContext context,
    ISemanticDogSearchService semanticDogSearchService,
    IGeocodingService geocodingService,
    IDistanceService distanceService) : IAdoptionCopilotToolService
{
    private static readonly string[] CalmSignals =
    {
        "calm",
        "quiet",
        "gentle",
        "relaxed",
        "easy-going",
        "easy going",
        "low energy",
        "slow walks",
        "short walks",
        "short daily walks",
        "indoor rest",
        "settles",
        "settle",
        "quiet routine",
        "predictable",
        "relaxed evenings"
    };

    private static readonly string[] ActiveConflictSignals =
    {
        "active",
        "energetic",
        "playful",
        "running",
        "run",
        "high energy",
        "very active",
        "longer walks",
        "space to run",
        "outdoor play"
    };

    private static readonly HashSet<string> AllowedDisplayTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "Short walks",
        "Indoor rest",
        "Settles quickly",
        "Quiet routine",
        "Small size",
        "Medium size",
        "Daily walks",
        "Longer walks",
        "Outdoor play",
        "Training games",
        "Active owner fit",
        "Gentle handling",
        "Family routine fit",
        "Better with older children",
        "Calm near cats",
        "Redirectable around cats",
        "Needs slow cat introductions",
        "Not suitable with cats",
        "Ask shelter about cats",
        "No cat history found",
        "Calm dog company",
        "Respectful around dogs",
        "Playful dog friends",
        "Needs slow dog introductions",
        "Needs calm dog introductions",
        "Gentle play style",
        "Not too energetic",
        "Not suited to pushy dogs",
        "May overwhelm senior dog",
        "May prefer only dog",
        "Ask shelter about senior dog fit",
        "Ask shelter about sensitive dog fit",
        "No dog compatibility history found",
        "Beginner-suitable routine",
        "Patient adopter needed",
        "Reserved - availability may change",
        "Not ideal for young children",
        "Needs calm children",
        "Ask shelter about children",
        "May overwhelm sensitive dogs",
        "Very energetic",
        "Higher activity needs",
        "Prefers shorter walks",
        "Ask shelter about apartment fit",
        "Large dog - confirm apartment fit",
        "Needs more space",
        "Missing behavior details",
        "Lower activity fit",
        "Space to run"
    };

    public async Task<AdoptionCopilotToolSearchResult> SearchDogsAsync(
        string adopterUserId,
        AdoptionCopilotSearchDogsArgs args,
        CancellationToken cancellationToken = default)
    {
        NormalizeOptionalArguments(args);
        var intent = AnalyzeIntent(args);
        var requestedCount = intent.Limit > 0 ? intent.Limit : args.Count;
        var count = Math.Clamp(requestedCount <= 0 ? 16 : Math.Max(requestedCount, 12), 1, 20);
        var appliedConstraints = BuildAppliedConstraints(args, intent);
        var sizes = ParseSizes(args.Sizes);
        var statuses = ParseStatuses(args.Statuses);
        var coatColors = ParseCoatColors(args.CoatColors);

        if (args.Statuses?.Count > 0 && statuses.Count == 0)
        {
            return new AdoptionCopilotToolSearchResult(
                [],
                appliedConstraints,
                false,
                "No dogs matched the requested status while keeping PawConnect public-safe filters.");
        }

        GeocodingResult? origin = null;
        if (!string.IsNullOrWhiteSpace(args.NearLocationText))
        {
            origin = await geocodingService.FindCoordinatesAsync(args.NearLocationText.Trim());
            if (origin is null)
            {
                return new AdoptionCopilotToolSearchResult(
                    [],
                    appliedConstraints,
                    false,
                    "Could not find the requested nearby location.");
            }
        }

        var dogs = await context.Dogs
            .Include(dog => dog.Shelter)
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .Include(dog => dog.Images)
            .AsNoTracking()
            .Where(dog => dog.Status == DogStatus.Available || dog.Status == DogStatus.Reserved)
            .ToListAsync(cancellationToken);

        var hardFiltered = new List<(Dog Dog, double? DistanceKm)>();
        foreach (var dog in dogs)
        {
            if (!MatchesHardFilters(dog, args, sizes, statuses, coatColors, origin, out var distanceKm))
            {
                continue;
            }

            hardFiltered.Add((dog, distanceKm));
        }

        if (hardFiltered.Count == 0)
        {
            var emptyReason = !string.IsNullOrWhiteSpace(args.Neighborhood)
                ? $"No dogs found in {args.Neighborhood.Trim()}."
                : "No dogs matched the exact constraints.";

            return new AdoptionCopilotToolSearchResult(
                [],
                appliedConstraints,
                false,
                emptyReason);
        }

        var semanticById = await GetSemanticRankingsAsync(adopterUserId, args, origin, count, cancellationToken);
        var queryTerms = BuildQueryTerms(args);
        var candidates = OrderCopilotCandidates(
            hardFiltered
            .Select(item => BuildCandidate(item.Dog, item.DistanceKm, args, intent, queryTerms, semanticById))
            .ToList(),
            intent,
            count)
            .ToList();

        if (IsNearestSort(args.Sort) && origin is not null)
        {
            candidates = candidates
                .OrderBy(candidate => candidate.DistanceKm ?? double.MaxValue)
                .ThenBy(candidate => candidate, Comparer<AdoptionCopilotToolDogCandidate>.Create((left, right) => CompareCopilotCandidates(left, right, intent)))
                .ThenBy(candidate => candidate.Dog.Name)
                .Take(count)
                .ToList();
        }

        return new AdoptionCopilotToolSearchResult(
            candidates,
            appliedConstraints,
            candidates.Any(candidate => semanticById.ContainsKey(candidate.DogId)));
    }

    private static IEnumerable<AdoptionCopilotToolDogCandidate> OrderCopilotCandidates(
        IEnumerable<AdoptionCopilotToolDogCandidate> candidates,
        CopilotIntent intent,
        int count)
    {
        return candidates
            .OrderBy(candidate => candidate, Comparer<AdoptionCopilotToolDogCandidate>.Create((left, right) => CompareCopilotCandidates(left, right, intent)))
            .Take(count);
    }

    private static int CompareCopilotCandidates(
        AdoptionCopilotToolDogCandidate left,
        AdoptionCopilotToolDogCandidate right,
        CopilotIntent intent)
    {
        var scoreDifference = Math.Abs(left.ScorePercent - right.ScorePercent);
        if (scoreDifference <= 3)
        {
            var signalComparison = CalculateVisibleRankingSignal(right, intent)
                .CompareTo(CalculateVisibleRankingSignal(left, intent));
            if (signalComparison != 0)
            {
                return signalComparison;
            }
        }

        var scoreComparison = right.ScorePercent.CompareTo(left.ScorePercent);
        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        var fallbackSignalComparison = CalculateVisibleRankingSignal(right, intent)
            .CompareTo(CalculateVisibleRankingSignal(left, intent));
        if (fallbackSignalComparison != 0)
        {
            return fallbackSignalComparison;
        }

        return string.Compare(left.Dog.Name, right.Dog.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateVisibleRankingSignal(AdoptionCopilotToolDogCandidate candidate, CopilotIntent intent)
    {
        var displayTags = (candidate.DisplayTags ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cautionTags = (candidate.CautionTags ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var signal = 0;

        if (IsModerateApartmentIntent(intent))
        {
            signal += displayTags.Contains("Longer walks") ? 8 : 0;
            signal += displayTags.Contains("Daily walks") ? 5 : 0;
            signal += displayTags.Contains("Medium size") ? 7 : 0;
            signal += displayTags.Contains("Small size") ? 6 : 0;
            signal += displayTags.Contains("Settles quickly") ? 6 : 0;
            signal += displayTags.Contains("Indoor rest") ? 3 : 0;
            signal += displayTags.Contains("Quiet routine") ? 3 : 0;

            signal -= cautionTags.Contains("Needs more space") ? 8 : 0;
            signal -= cautionTags.Contains("Ask shelter about apartment fit") ? 5 : 0;
            signal -= cautionTags.Contains("Large dog - confirm apartment fit") ? 6 : 0;
            signal -= cautionTags.Contains("Higher activity needs") ? 6 : 0;
            signal -= cautionTags.Contains("Patient adopter needed") ? 4 : 0;
            signal -= cautionTags.Contains("Prefers shorter walks") ? 5 : 0;
        }
        else
        {
            signal += displayTags.Count * 3;
            signal -= cautionTags.Count * 2;
        }

        if (!DogImageUrlValidator.GetRealDogImages(candidate.Dog.Images).Any())
        {
            signal -= 1;
        }

        return signal;
    }

    public async Task<AdoptionCopilotProfileToolResult?> GetAdopterProfileSummaryAsync(
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        var profile = await context.AdopterProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ApplicationUserId == adopterUserId, cancellationToken);

        return profile is null
            ? null
            : new AdoptionCopilotProfileToolResult(
                EmptyToNull(profile.City),
                profile.HousingType.ToString(),
                profile.HasYard,
                profile.HasOtherPets,
                profile.HasChildren,
                EmptyToNull(profile.ExperienceWithDogs));
    }

    public async Task<AdoptionCopilotPreferenceToolResult> GetFavoriteAndRecentPreferencesAsync(
        string adopterUserId,
        CancellationToken cancellationToken = default)
    {
        var favoriteDogs = await context.FavoriteDogs
            .Include(favorite => favorite.Dog)
            .ThenInclude(dog => dog!.DogBreed)
            .Include(favorite => favorite.Dog)
            .ThenInclude(dog => dog!.SecondaryBreed)
            .Include(favorite => favorite.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .AsNoTracking()
            .Where(favorite => favorite.AdopterId == adopterUserId &&
                favorite.Dog != null &&
                (favorite.Dog.Status == DogStatus.Available || favorite.Dog.Status == DogStatus.Reserved))
            .Select(favorite => favorite.Dog!)
            .ToListAsync(cancellationToken);

        var recentDogs = await context.RecentlyViewedDogs
            .Include(view => view.Dog)
            .ThenInclude(dog => dog!.DogBreed)
            .Include(view => view.Dog)
            .ThenInclude(dog => dog!.SecondaryBreed)
            .Include(view => view.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .AsNoTracking()
            .Where(view => view.AdopterId == adopterUserId &&
                view.Dog != null &&
                (view.Dog.Status == DogStatus.Available || view.Dog.Status == DogStatus.Reserved))
            .OrderByDescending(view => view.ViewedAt)
            .Take(10)
            .Select(view => view.Dog!)
            .ToListAsync(cancellationToken);

        var dogs = favoriteDogs.Concat(recentDogs).ToList();
        return new AdoptionCopilotPreferenceToolResult(
            MostCommon(dogs.Select(dog => dog.Size.ToString())),
            MostCommon(dogs.Select(DogBreedFormatter.Format)),
            MostCommon(dogs.Select(dog => dog.Shelter?.City)));
    }

    public async Task<AdoptionCopilotToolDogCandidate?> GetDogDetailsPublicAsync(
        int dogId,
        CancellationToken cancellationToken = default)
    {
        var dog = await context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .Include(d => d.Images)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dogId &&
                (d.Status == DogStatus.Available || d.Status == DogStatus.Reserved),
                cancellationToken);

        return dog is null
            ? null
            : BuildCandidate(
                dog,
                null,
                new AdoptionCopilotSearchDogsArgs(),
                AnalyzeIntent(new AdoptionCopilotSearchDogsArgs()),
                [],
                new Dictionary<int, SemanticDogSearchResult>());
    }

    private async Task<Dictionary<int, SemanticDogSearchResult>> GetSemanticRankingsAsync(
        string adopterUserId,
        AdoptionCopilotSearchDogsArgs args,
        GeocodingResult? origin,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            var parsedSizes = ParseSizes(args.Sizes);
            var parsedStatuses = ParseStatuses(args.Statuses);
            var singleSize = parsedSizes.Count == 1 ? parsedSizes.Single() : (DogSize?)null;
            var singleStatus = parsedStatuses.Count == 1 ? parsedStatuses.Single() : (DogStatus?)null;
            var options = new SemanticDogSearchOptions
            {
                Size = singleSize,
                Status = singleStatus,
                Neighborhood = EmptyToNull(args.Neighborhood),
                Location = EmptyToNull(args.City),
                CoatColors = args.CoatColors,
                OriginLatitude = origin?.Latitude,
                OriginLongitude = origin?.Longitude,
                RadiusKm = args.RadiusKm
            };

            var query = string.IsNullOrWhiteSpace(args.Query)
                ? string.Join(' ', (args.BehaviorTerms ?? []).Concat(args.TemperamentTags ?? []))
                : args.Query!;
            var results = await semanticDogSearchService.SearchDogsAsync(query, adopterUserId, Math.Max(count, 12), options, cancellationToken);
            return results.ToDictionary(result => result.DogId);
        }
        catch
        {
            return [];
        }
    }

    private bool MatchesHardFilters(
        Dog dog,
        AdoptionCopilotSearchDogsArgs args,
        IReadOnlySet<DogSize> sizes,
        IReadOnlySet<DogStatus> statuses,
        IReadOnlySet<string> coatColors,
        GeocodingResult? origin,
        out double? distanceKm)
    {
        distanceKm = null;
        if (dog.Status is not (DogStatus.Available or DogStatus.Reserved))
        {
            return false;
        }

        if (statuses.Count > 0 && !statuses.Contains(dog.Status))
        {
            return false;
        }

        if (sizes.Count > 0 && !sizes.Contains(dog.Size))
        {
            return false;
        }

        var dogBreed = DogBreedFormatter.Format(dog);
        if (args.Breeds?.Count > 0 &&
            !args.Breeds.Any(breed => string.Equals(dogBreed, breed, StringComparison.OrdinalIgnoreCase) ||
                dogBreed.Contains(breed, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (coatColors.Count > 0)
        {
            var dogCoatColor = DogCoatColorOptions.Normalize(dog.CoatColor);
            if (string.IsNullOrWhiteSpace(dogCoatColor) || !coatColors.Contains(dogCoatColor))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(args.City) &&
            !Contains(dog.Shelter?.City, args.City) &&
            !Contains(dog.Location, args.City))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(args.Neighborhood) &&
            !string.Equals(dog.Shelter?.Neighborhood, args.Neighborhood.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(args.ShelterName) &&
            !Contains(dog.Shelter?.Name, args.ShelterName))
        {
            return false;
        }

        if (!MatchesAgeConstraint(dog, args))
        {
            return false;
        }

        if (HasConflictingEnergyForCalmRequest(dog, args))
        {
            return false;
        }

        if (origin is not null)
        {
            if (dog.Shelter?.Latitude is null || dog.Shelter.Longitude is null)
            {
                return false;
            }

            distanceKm = distanceService.CalculateDistanceKm(
                origin.Latitude,
                origin.Longitude,
                dog.Shelter.Latitude.Value,
                dog.Shelter.Longitude.Value);

            if (args.RadiusKm.HasValue && distanceKm > args.RadiusKm.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasConflictingEnergyForCalmRequest(Dog dog, AdoptionCopilotSearchDogsArgs args)
    {
        if (!IsCalmRequest(args))
        {
            return false;
        }

        var searchableText = BuildSearchableDogText(dog);
        return HasActiveSignal(searchableText) && !HasCalmSignal(searchableText);
    }

    private static AdoptionCopilotToolDogCandidate BuildCandidate(
        Dog dog,
        double? distanceKm,
        AdoptionCopilotSearchDogsArgs args,
        CopilotIntent intent,
        IReadOnlyList<string> queryTerms,
        IReadOnlyDictionary<int, SemanticDogSearchResult> semanticById)
    {
        var score = 34;
        var reasons = new List<string>();
        var reservedOnlyRequest = ParseStatuses(args.Statuses).SetEquals([DogStatus.Reserved]);
        var filterOnlyRequest = IsFilterOnlyRequest(args, intent);
        var hasHardConstraints = HasExplicitHardConstraints(args);
        if (semanticById.TryGetValue(dog.Id, out var semanticResult))
        {
            // Semantic similarity is supporting evidence; deterministic public-safe matching remains the source of truth.
            score += Math.Clamp((semanticResult.ScorePercent - 55) / 7, 0, 5);
        }

        var searchableText = BuildSearchableDogText(dog);
        var keywordMatches = queryTerms.Count(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));
        score += Math.Min(6, keywordMatches * 2);

        if (dog.Status == DogStatus.Available)
        {
            score += 2;
        }
        else if (dog.Status == DogStatus.Reserved)
        {
            score += reservedOnlyRequest ? 1 : -1;
            AddReason(reasons, "Reserved - availability may change.");
        }

        if (distanceKm.HasValue)
        {
            score += distanceKm.Value <= 5 ? 6 : distanceKm.Value <= 25 ? 4 : 1;
            reasons.Add($"{distanceKm.Value:0.#} km away");
        }

        if (args.MaxAgeYears is > 0 || args.MinAgeYears is > 0)
        {
            AddReason(reasons, "Age fits your search");
            score += 7;
        }

        var parsedSizes = ParseSizes(args.Sizes);
        if (parsedSizes.Contains(dog.Size))
        {
            AddReason(reasons, "Size matches your search");
            score += 11;
        }

        var parsedCoatColors = ParseCoatColors(args.CoatColors);
        var dogCoatColor = DogCoatColorOptions.Normalize(dog.CoatColor);
        if (!string.IsNullOrWhiteSpace(dogCoatColor) && parsedCoatColors.Contains(dogCoatColor))
        {
            AddReason(reasons, $"Coat color: {dogCoatColor}");
            score += 11;
        }

        AddLifestyleScores(dog, args, searchableText, reasons, ref score);

        if (IsApartmentRequest(args) && dog.Size is DogSize.Small or DogSize.Medium)
        {
            AddReason(reasons, dog.Size == DogSize.Small ? "Small size" : "Medium size");
            score += dog.Size == DogSize.Small ? 6 : 5;
        }

        if (IsYardRequest(args) && dog.Size is DogSize.Medium or DogSize.Large)
        {
            AddReason(reasons, dog.Size == DogSize.Large ? "Large size" : "Medium size");
            score += dog.Size == DogSize.Large ? 5 : 2;
        }

        if (!string.IsNullOrWhiteSpace(args.City) &&
            (Contains(dog.Shelter?.City, args.City) || Contains(dog.Location, args.City)))
        {
            AddReason(reasons, $"In {args.City.Trim()}");
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(args.Neighborhood) &&
            string.Equals(dog.Shelter?.Neighborhood, args.Neighborhood.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            AddReason(reasons, $"In {args.Neighborhood.Trim()}");
            score += 10;
        }

        if (semanticResult is not null)
        {
            foreach (var reason in semanticResult.Reasons)
            {
                if (IsSemanticReasonRelevantToCopilotArgs(reason, args))
                {
                    AddReason(reasons, reason);
                }
            }
        }

        if (reasons.Count == 0)
        {
            AddReason(reasons, "Review shelter details");
        }

        if (HasSparseProfileText(searchableText))
        {
            score -= 4;
        }

        var safeReasons = reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var evidence = ExtractDogEvidence(dog, args, intent, searchableText, safeReasons);
        score += CalculateIntentEvidenceScore(intent, evidence, dog.Status);
        score = ApplyCompatibilityEvidenceCaps(intent, evidence, score);
        score = ApplyHomeActivityEvidenceCaps(intent, evidence, score);

        var safeScore = filterOnlyRequest
            ? GetFilterOnlyScore(dog.Status, reservedOnlyRequest)
            : CalibrateRecommendationScore(score, intent, evidence);
        if (hasHardConstraints && !filterOnlyRequest && !HasConflictingEnergyForCalmRequest(dog, args))
        {
            safeScore = ApplySatisfiedHardConstraintFloor(safeScore, args, evidence);
        }

        if (!filterOnlyRequest)
        {
            safeScore = ApplyFinalVisibleDifferentiation(dog, safeScore, intent, evidence);
        }

        var matchLabel = filterOnlyRequest ? "Exact match" : GetMatchLabel(safeScore);

        return new AdoptionCopilotToolDogCandidate(
            dog.Id,
            dog,
            safeScore,
            matchLabel,
            safeReasons.Take(3).ToList(),
            BuildSuggestedNextAction(dog, intent, evidence),
            distanceKm,
            evidence.SupportedDisplayTags,
            evidence.CautionEvidence,
            evidence.EvidenceSummary,
            evidence.PositiveEvidenceItems,
            evidence.CautionEvidenceItems,
            evidence.NegativeEvidenceItems,
            evidence.MissingEvidenceItems);
    }

    private static string BuildSuggestedNextAction(Dog dog, CopilotIntent intent, CopilotDogEvidence evidence)
    {
        var displayTags = evidence.SupportedDisplayTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cautionTags = evidence.CautionEvidence.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (IsModerateApartmentIntent(intent))
        {
            if (cautionTags.Contains("Large dog - confirm apartment fit"))
            {
                return $"Confirm with the shelter whether {dog.Name}'s size and exercise needs fit apartment living.";
            }

            if (cautionTags.Contains("Needs more space"))
            {
                return $"Ask the shelter how much space {dog.Name} needs alongside longer walks.";
            }

            if (cautionTags.Contains("Ask shelter about apartment fit"))
            {
                return $"Ask the shelter to confirm {dog.Name}'s apartment routine and longer-walk needs.";
            }

            if (displayTags.Contains("Longer walks") &&
                (displayTags.Contains("Medium size") || displayTags.Contains("Small size") || displayTags.Contains("Settles quickly")))
            {
                return $"Review {dog.Name}'s profile for apartment routine and longer-walk details.";
            }
        }

        return "View the dog profile and check the shelter details.";
    }

    private static int ApplyCompatibilityEvidenceCaps(
        CopilotIntent intent,
        CopilotDogEvidence evidence,
        int score)
    {
        if (intent.PrimaryIntent == "Compatibility")
        {
            if (HasUncertainPrimaryEvidence(evidence))
            {
                score = Math.Min(score, 80);
            }

            if (!HasDirectEvidenceForPrimaryCompatibility(intent, evidence))
            {
                score = evidence.GenericEvidence.Count > 0
                    ? Math.Min(score, 70)
                    : Math.Min(score, 84);
            }

            if (HasPrimaryCompatibilityCaution(intent, evidence))
            {
                score = Math.Min(score, 92);
            }
        }

        if (IsCompatibilityTarget(intent, "Cats", "SmallAnimals"))
        {
            if (evidence.CautionEvidence.Contains("Not suitable with cats", StringComparer.OrdinalIgnoreCase))
            {
                return Math.Min(score, 54);
            }

            if (evidence.SupportedDisplayTags.Contains("Needs slow cat introductions", StringComparer.OrdinalIgnoreCase))
            {
                return Math.Min(score, 82);
            }

            if (!HasUsefulCatEvidence(evidence))
            {
                return Math.Min(score, 62);
            }
        }

        if (IsCompatibilityTarget(intent, "Children", "OlderChildren"))
        {
            var hasDirectChildEvidence = HasDirectChildEvidence(evidence);
            if (IsCompatibilityTarget(intent, "Children") &&
                intent.SecondarySignals.Contains("young children", StringComparer.OrdinalIgnoreCase) &&
                evidence.DirectEvidence.Count > 0 &&
                evidence.DirectEvidence.All(tag => tag == "Better with older children"))
            {
                return Math.Min(score, 78);
            }

            if (IsCompatibilityTarget(intent, "Children") &&
                intent.SecondarySignals.Contains("young children", StringComparer.OrdinalIgnoreCase) &&
                evidence.DirectEvidence.Count > 0 &&
                evidence.DirectEvidence.All(tag => tag is "Better with older children" or "Family routine fit"))
            {
                return Math.Min(score, 88);
            }

            if (IsCompatibilityTarget(intent, "Children") &&
                evidence.DirectEvidence.Count > 0 &&
                evidence.DirectEvidence.All(tag => tag == "Better with older children"))
            {
                return Math.Min(score, 84);
            }

            if (!hasDirectChildEvidence)
            {
                if (evidence.IndirectEvidence.Count > 0)
                {
                    return Math.Min(score, 78);
                }

                if (evidence.GenericEvidence.Count > 0)
                {
                    return Math.Min(score, 68);
                }

                return Math.Min(score, 64);
            }
        }

        if (intent.PrimaryIntent == "Compatibility" &&
            IsCompatibilityTarget(intent, "OtherDogs") &&
            !evidence.SupportedDisplayTags.Any(tag => tag.Contains("dog", StringComparison.OrdinalIgnoreCase)))
        {
            return Math.Min(score, 64);
        }

        if (IsCompatibilityTarget(intent, "SeniorDog", "SensitiveDog"))
        {
            if (evidence.CautionEvidence.Contains("May overwhelm senior dog", StringComparer.OrdinalIgnoreCase) ||
                evidence.CautionEvidence.Contains("May overwhelm sensitive dogs", StringComparer.OrdinalIgnoreCase) ||
                evidence.CautionEvidence.Contains("Very energetic", StringComparer.OrdinalIgnoreCase) ||
                evidence.CautionEvidence.Contains("May prefer only dog", StringComparer.OrdinalIgnoreCase))
            {
                return Math.Min(score, 58);
            }

            var hasDirectEvidence = HasAnySeniorDogDirectEvidence(evidence);
            var hasStrongDirectEvidence = HasStrongDirectSeniorDogEvidence(evidence);
            var hasCaution = HasSeniorDogEvidenceCaution(evidence);
            var hasMissingEvidence = HasMissingPrimaryCompatibilityEvidence(evidence);

            if (!hasDirectEvidence && hasMissingEvidence)
            {
                return Math.Min(score, 76);
            }

            if (!hasDirectEvidence && evidence.IndirectEvidence.Count > 0)
            {
                return Math.Min(score, 82);
            }

            if (!hasDirectEvidence && evidence.GenericEvidence.Count > 0)
            {
                return Math.Min(score, 68);
            }

            if (!hasDirectEvidence)
            {
                return Math.Min(score, 72);
            }

            if (hasCaution)
            {
                return Math.Min(score, hasStrongDirectEvidence ? 89 : 88);
            }

            if (!hasStrongDirectEvidence)
            {
                return Math.Min(score, 86);
            }
        }

        if (IsCompatibilityTarget(intent, "YoungDog") &&
            !evidence.SupportedDisplayTags.Any(tag =>
                tag is "Playful dog friends" or "Active owner fit" or "Outdoor play" or "Training games" or "Calm dog company" or "Needs slow dog introductions"))
        {
            return Math.Min(score, 68);
        }

        return score;
    }

    private static int ApplyHomeActivityEvidenceCaps(
        CopilotIntent intent,
        CopilotDogEvidence evidence,
        int score)
    {
        if (intent.PrimaryIntent is not ("HomeSuitability" or "ActivityLevel"))
        {
            return score;
        }

        if (IsModerateApartmentIntent(intent))
        {
            var directCount = evidence.DirectEvidence.Count(tag =>
                tag is "Longer walks" or "Daily walks" or "Medium size" or "Small size");
            return directCount switch
            {
                >= 2 => Math.Min(score, 86),
                1 => Math.Min(score, 76),
                _ => Math.Min(score, 62)
            };
        }

        if (intent.HomeType == "Apartment" || intent.ActivityLevel == "Low")
        {
            var directCount = evidence.DirectEvidence.Count(tag =>
                tag is "Short walks" or "Indoor rest" or "Settles quickly" or "Quiet routine" or
                    "Small size" or "Medium size" or "Lower activity fit");
            return directCount switch
            {
                >= 4 => score,
                3 => Math.Min(score, 92),
                2 => Math.Min(score, 86),
                1 => Math.Min(score, 78),
                _ => Math.Min(score, 68)
            };
        }

        if (intent.HomeType == "House with yard" || intent.ActivityLevel == "High")
        {
            var directCount = evidence.DirectEvidence.Count(tag =>
                tag is "Longer walks" or "Outdoor play" or "Training games" or "Active owner fit" or "Space to run");
            return directCount switch
            {
                >= 3 => score,
                2 => Math.Min(score, 90),
                1 => Math.Min(score, 82),
                _ => Math.Min(score, 70)
            };
        }

        return score;
    }

    private static int CalibrateRecommendationScore(int rawScore, CopilotIntent intent, CopilotDogEvidence evidence)
    {
        var calibrated = rawScore switch
        {
            >= 120 => 88 + Math.Min(4, (rawScore - 120) / 8),
            >= 105 => 82 + Math.Min(6, (rawScore - 105) / 3),
            >= 90 => 75 + Math.Min(7, (rawScore - 90) / 3),
            >= 75 => 67 + Math.Min(8, (rawScore - 75) / 2),
            >= 60 => 57 + Math.Min(10, (rawScore - 60) * 2 / 3),
            >= 45 => 45 + Math.Min(12, (rawScore - 45) * 4 / 5),
            _ => Math.Clamp(rawScore, 25, 44)
        };

        var directEvidenceCount = evidence.DirectEvidence.Count;
        var indirectEvidenceCount = evidence.IndirectEvidence.Count;
        var primaryEvidenceWeight = CalculatePrimaryIntentEvidenceWeight(intent, evidence);

        calibrated += primaryEvidenceWeight switch
        {
            >= 30 => 5,
            >= 22 => 4,
            >= 16 => 2,
            >= 8 => 1,
            _ => 0
        };

        calibrated += directEvidenceCount switch
        {
            >= 4 => 5,
            3 => 4,
            2 => 2,
            1 => 1,
            _ => 0
        };

        calibrated += Math.Min(2, indirectEvidenceCount / 2);

        if (HasStrongPrimaryEvidence(intent, evidence))
        {
            calibrated += 2;
        }

        if (HasUncertainPrimaryEvidence(evidence))
        {
            calibrated = Math.Min(calibrated, 80);
        }

        calibrated -= CalculateCautionPenalty(evidence);
        calibrated = ApplyIntentConfidenceCaps(intent, evidence, calibrated);

        return Math.Clamp(calibrated, 25, 92);
    }

    private static int ApplyIntentConfidenceCaps(CopilotIntent intent, CopilotDogEvidence evidence, int scorePercent)
    {
        var hasMaterialCaution = HasMaterialCaution(evidence);
        var evidenceWeight = CalculatePrimaryIntentEvidenceWeight(intent, evidence);

        if (IsModerateApartmentIntent(intent))
        {
            var cap = evidenceWeight switch
            {
                >= 22 => hasMaterialCaution ? 78 : 84,
                >= 14 => hasMaterialCaution ? 72 : 78,
                >= 8 => hasMaterialCaution ? 64 : 70,
                _ => hasMaterialCaution ? 52 : 60
            };

            return Math.Min(scorePercent, cap);
        }

        if (intent.PrimaryIntent is "HomeSuitability" or "ActivityLevel" &&
            (intent.HomeType == "Apartment" || intent.ActivityLevel == "Low"))
        {
            var cap = evidenceWeight switch
            {
                >= 30 => hasMaterialCaution ? 80 : 85,
                >= 22 => hasMaterialCaution ? 76 : 82,
                >= 16 => hasMaterialCaution ? 72 : 78,
                >= 8 => hasMaterialCaution ? 64 : 70,
                _ => hasMaterialCaution ? 52 : 60
            };

            return Math.Min(scorePercent, cap);
        }

        if (intent.PrimaryIntent is "HomeSuitability" or "ActivityLevel" &&
            (intent.HomeType == "House with yard" || intent.ActivityLevel == "High"))
        {
            var cap = evidenceWeight switch
            {
                >= 30 => hasMaterialCaution ? 80 : 86,
                >= 22 => hasMaterialCaution ? 78 : 84,
                >= 16 => hasMaterialCaution ? 72 : 78,
                >= 8 => hasMaterialCaution ? 64 : 70,
                _ => hasMaterialCaution ? 54 : 62
            };

            return Math.Min(scorePercent, cap);
        }

        return scorePercent;
    }

    private static int CalculatePrimaryIntentEvidenceWeight(CopilotIntent intent, CopilotDogEvidence evidence)
    {
        if (IsModerateApartmentIntent(intent))
        {
            return evidence.DirectEvidence.Sum(tag => tag switch
            {
                "Longer walks" => 11,
                "Daily walks" => 7,
                "Medium size" => 5,
                "Small size" => 4,
                "Settles quickly" => 3,
                _ => 0
            });
        }

        if (intent.PrimaryIntent is "HomeSuitability" or "ActivityLevel" &&
            (intent.HomeType == "Apartment" || intent.ActivityLevel == "Low"))
        {
            return evidence.DirectEvidence.Sum(tag => tag switch
            {
                "Quiet routine" => 10,
                "Short walks" => 9,
                "Indoor rest" => 8,
                "Settles quickly" => 8,
                "Lower activity fit" => 8,
                "Small size" => 5,
                "Medium size" => 4,
                _ => 0
            });
        }

        if (intent.PrimaryIntent is "HomeSuitability" or "ActivityLevel" &&
            (intent.HomeType == "House with yard" || intent.ActivityLevel == "High"))
        {
            return evidence.DirectEvidence.Sum(tag => tag switch
            {
                "Outdoor play" => 10,
                "Longer walks" => 9,
                "Training games" => 8,
                "Active owner fit" => 8,
                "Space to run" => 8,
                "Medium size" => 3,
                _ => 0
            });
        }

        return evidence.DirectEvidence.Count * 8 + evidence.IndirectEvidence.Count * 3;
    }

    private static bool HasMaterialCaution(CopilotDogEvidence evidence)
    {
        return evidence.MissingEvidence.Count > 0 ||
            evidence.CautionEvidence.Any(caution =>
                caution is not "Reserved - availability may change");
    }

    private static bool IsModerateApartmentIntent(CopilotIntent intent)
    {
        return intent.PrimaryIntent is "HomeSuitability" or "ActivityLevel" &&
            intent.HomeType == "Apartment" &&
            intent.ActivityLevel is ("Medium" or "High");
    }

    private static int ApplySatisfiedHardConstraintFloor(
        int scorePercent,
        AdoptionCopilotSearchDogsArgs args,
        CopilotDogEvidence evidence)
    {
        var hardConstraintCount = CountExplicitHardConstraints(args);
        if (hardConstraintCount == 0)
        {
            return scorePercent;
        }

        var directEvidenceCount = evidence.DirectEvidence.Count;
        var baseFloor = 55 + Math.Min(10, hardConstraintCount * 3);
        var directEvidenceFloor = directEvidenceCount switch
        {
            >= 4 => 13,
            3 => 10,
            2 => 7,
            1 => 4,
            _ => 0
        };
        var cautionAdjustment = Math.Min(12, CalculateCautionPenalty(evidence) / 2);
        var floor = Math.Clamp(baseFloor + directEvidenceFloor - cautionAdjustment, 55, 80);

        return Math.Max(scorePercent, floor);
    }

    private static int CalculateCautionPenalty(CopilotDogEvidence evidence)
    {
        var penalty = 0;
        foreach (var caution in evidence.CautionEvidence.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            penalty += caution switch
            {
                "Reserved - availability may change" => 1,
                "Patient adopter needed" => 7,
                "Missing behavior details" => 5,
                "Very energetic" => 14,
                "Higher activity needs" => 10,
                "Ask shelter about apartment fit" => 8,
                "Large dog - confirm apartment fit" => 5,
                "Prefers shorter walks" => 6,
                "Needs more space" => 14,
                "May overwhelm senior dog" or "May overwhelm sensitive dogs" => 16,
                "May prefer only dog" => 14,
                "Not suitable with cats" => 18,
                "Not ideal for young children" => 12,
                _ when caution.StartsWith("Ask shelter", StringComparison.OrdinalIgnoreCase) => 7,
                _ when caution.StartsWith("No ", StringComparison.OrdinalIgnoreCase) => 8,
                _ => 4
            };
        }

        foreach (var missing in evidence.MissingEvidence.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            penalty += missing.StartsWith("No ", StringComparison.OrdinalIgnoreCase) ? 6 : 4;
        }

        return Math.Min(24, penalty);
    }

    private static int ApplyFinalVisibleDifferentiation(Dog dog, int scorePercent, CopilotIntent intent, CopilotDogEvidence evidence)
    {
        var penalty = 0;
        var positiveBonus = 0;
        var cautions = evidence.CautionEvidence.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var displayTags = evidence.SupportedDisplayTags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (cautions.Contains("Higher activity needs"))
        {
            penalty += 4;
        }

        if (cautions.Contains("Needs more space"))
        {
            penalty += 5;
        }

        if (cautions.Contains("Patient adopter needed"))
        {
            penalty += 5;
        }

        if (cautions.Contains("Reserved - availability may change"))
        {
            penalty += 2;
        }

        if (cautions.Contains("Ask shelter about apartment fit"))
        {
            penalty += 3;
        }

        if (cautions.Contains("Large dog - confirm apartment fit"))
        {
            penalty += 3;
        }

        if (cautions.Contains("Prefers shorter walks"))
        {
            penalty += 3;
        }

        if (cautions.Contains("Missing behavior details") ||
            evidence.MissingEvidence.Count > 0)
        {
            penalty += 2;
        }

        if (!DogImageUrlValidator.GetRealDogImages(dog.Images).Any())
        {
            penalty += 3;
        }

        var apartmentSupport = IsModerateApartmentIntent(intent)
            ? CountApartmentSupportSignals(displayTags)
            : 0;

        if (IsModerateApartmentIntent(intent))
        {
            positiveBonus += displayTags.Contains("Longer walks") ? 2 : 0;
            positiveBonus += displayTags.Contains("Daily walks") ? 1 : 0;
            positiveBonus += displayTags.Contains("Medium size") ? 3 : 0;
            positiveBonus += displayTags.Contains("Small size") ? 3 : 0;
            positiveBonus += displayTags.Contains("Settles quickly") ? 3 : 0;
            positiveBonus += displayTags.Contains("Indoor rest") ? 1 : 0;
            positiveBonus += displayTags.Contains("Quiet routine") ? 1 : 0;
            positiveBonus = Math.Min(6, positiveBonus);

            if (cautions.Contains("Needs more space"))
            {
                if (apartmentSupport == 0)
                {
                    penalty += 3;
                }
                else
                {
                    penalty -= Math.Min(4, apartmentSupport * 2);
                }
            }

            if (cautions.Contains("Ask shelter about apartment fit") && apartmentSupport >= 2)
            {
                penalty -= 2;
            }
        }

        var adjustedScore = scorePercent + positiveBonus - Math.Min(10, Math.Max(0, penalty));
        if (IsModerateApartmentIntent(intent) && displayTags.Contains("Longer walks"))
        {
            var longerWalkFloor = apartmentSupport switch
            {
                >= 2 => 60,
                1 => 58,
                _ => cautions.Contains("Needs more space") ? 52 : 57
            };
            adjustedScore = Math.Max(adjustedScore, longerWalkFloor);
        }

        return Math.Clamp(adjustedScore, 25, 92);
    }

    private static int CountApartmentSupportSignals(IReadOnlySet<string> displayTags)
    {
        var count = 0;
        count += displayTags.Contains("Medium size") || displayTags.Contains("Small size") ? 1 : 0;
        count += displayTags.Contains("Settles quickly") ? 1 : 0;
        count += displayTags.Contains("Indoor rest") ? 1 : 0;
        count += displayTags.Contains("Quiet routine") ? 1 : 0;
        return count;
    }

    private static int GetFilterOnlyScore(DogStatus status, bool reservedOnlyRequest)
    {
        return status switch
        {
            DogStatus.Available => 72,
            DogStatus.Reserved when reservedOnlyRequest => 72,
            DogStatus.Reserved => 68,
            _ => 60
        };
    }

    private static bool HasStrongPrimaryEvidence(CopilotIntent intent, CopilotDogEvidence evidence)
    {
        if (intent.PrimaryIntent == "Compatibility")
        {
            return HasDirectEvidenceForPrimaryCompatibility(intent, evidence) && !HasPrimaryCompatibilityCaution(intent, evidence);
        }

        if (intent.PrimaryIntent is "HomeSuitability" or "ActivityLevel")
        {
            return evidence.DirectEvidence.Count >= 4;
        }

        return evidence.DirectEvidence.Count >= 2;
    }

    private static bool IsFilterOnlyRequest(AdoptionCopilotSearchDogsArgs args, CopilotIntent intent)
    {
        return HasExplicitHardConstraints(args) &&
            (!HasSoftSuitabilitySignals(args, intent) || IsClearlyFilterOnlyQuery(args));
    }

    private static bool IsClearlyFilterOnlyQuery(AdoptionCopilotSearchDogsArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
        {
            return false;
        }

        return HasExplicitHardConstraints(args) &&
            !ContainsAny(args.Query,
            [
                "apartment",
                "flat",
                "yard",
                "garden",
                "house",
                "calm",
                "quiet",
                "gentle",
                "friendly",
                "active",
                "playful",
                "energetic",
                "low activity",
                "short walk",
                "long walk",
                "longer walk",
                "daily walk",
                "regular walk",
                "brisk walk",
                "children",
                "kids",
                "cat",
                "other dog",
                "older dog",
                "sick dog",
                "beginner",
                "first time",
                "favorite",
                "similar"
            ]);
    }

    private static bool HasExplicitHardConstraints(AdoptionCopilotSearchDogsArgs args)
    {
        return CountExplicitHardConstraints(args) > 0;
    }

    private static int CountExplicitHardConstraints(AdoptionCopilotSearchDogsArgs args)
    {
        var count = 0;
        if (args.Sizes?.Count > 0)
        {
            count++;
        }

        if (args.Breeds?.Count > 0)
        {
            count++;
        }

        if (args.CoatColors?.Count > 0)
        {
            count++;
        }

        if (HasExplicitStatusConstraint(args))
        {
            count++;
        }

        if (!string.IsNullOrWhiteSpace(args.City) ||
            !string.IsNullOrWhiteSpace(args.Neighborhood) ||
            !string.IsNullOrWhiteSpace(args.ShelterName) ||
            !string.IsNullOrWhiteSpace(args.NearLocationText))
        {
            count++;
        }

        if (args.MaxAgeYears is > 0 || args.MinAgeYears is > 0)
        {
            count++;
        }

        return count;
    }

    private static bool HasSoftSuitabilitySignals(AdoptionCopilotSearchDogsArgs args, CopilotIntent intent)
    {
        return IsApartmentRequest(args) ||
            IsYardRequest(args) ||
            IsCalmRequest(args) ||
            HasChildrenRequest(args) ||
            HasCatRequest(args) ||
            HasOtherDogsRequest(args) ||
            HasSeniorOrSensitiveHouseholdDogRequest(args) ||
            HasYoungHouseholdDogRequest(args) ||
            !string.Equals(intent.PrimaryIntent, "Location", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(intent.PrimaryIntent, "Size", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(intent.PrimaryIntent, "Temperament", StringComparison.OrdinalIgnoreCase) &&
                (intent.PrimaryIntent is "Compatibility" or "HomeSuitability" or "ActivityLevel" or "ExperienceLevel") ||
            args.BehaviorTerms?.Count > 0 ||
            args.TemperamentTags?.Count > 0 ||
            args.Temperaments?.Count > 0 ||
            args.Compatibility?.Count > 0 ||
            !string.IsNullOrWhiteSpace(args.EnergyLevel) ||
            !string.IsNullOrWhiteSpace(args.ActivityLevel) ||
            !string.IsNullOrWhiteSpace(args.HomeType) ||
            !string.IsNullOrWhiteSpace(args.HousingPreference) ||
            args.ApartmentFriendly == true ||
            args.YardFriendly == true ||
            args.YardRequired == true ||
            args.NeedsYard == true ||
            args.GoodWithChildren == true ||
            args.GoodWithPets == true ||
            !string.IsNullOrWhiteSpace(args.ExperienceLevel) ||
            args.DesiredTraits?.Count > 0 ||
            args.MustHave?.Count > 0 ||
            args.NiceToHave?.Count > 0 ||
            args.AvoidTraits?.Count > 0 ||
            args.Avoid?.Count > 0 ||
            args.EvidenceToLookFor?.Count > 0;
    }

    private static bool HasExplicitStatusConstraint(AdoptionCopilotSearchDogsArgs args)
    {
        if (args.Statuses is null || args.Statuses.Count == 0)
        {
            return false;
        }

        var statuses = ParseStatuses(args.Statuses);
        return !statuses.SetEquals([DogStatus.Available, DogStatus.Reserved]);
    }

    private static bool HasUncertainPrimaryEvidence(CopilotDogEvidence evidence)
    {
        return evidence.SupportedDisplayTags.Any(tag =>
                tag.StartsWith("Ask shelter", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("No ", StringComparison.OrdinalIgnoreCase)) ||
            evidence.MissingEvidence.Count > 0;
    }

    private static bool HasPrimaryCompatibilityCaution(CopilotIntent intent, CopilotDogEvidence evidence)
    {
        if (evidence.CautionEvidence.Any(tag =>
            tag is not "Reserved - availability may change" and not "Missing behavior details"))
        {
            return true;
        }

        if (IsCompatibilityTarget(intent, "Cats", "SmallAnimals"))
        {
            return evidence.SupportedDisplayTags.Any(tag => tag is "Needs slow cat introductions");
        }

        if (IsCompatibilityTarget(intent, "Children", "OlderChildren"))
        {
            return evidence.SupportedDisplayTags.Any(tag => tag is "Better with older children" or "Needs calm children");
        }

        if (IsCompatibilityTarget(intent, "SeniorDog", "SensitiveDog", "OtherDogs"))
        {
            return evidence.SupportedDisplayTags.Any(tag =>
                tag is "Needs slow dog introductions" or "Needs calm dog introductions" or "Not suited to pushy dogs");
        }

        return false;
    }

    private static bool HasDirectEvidenceForPrimaryCompatibility(CopilotIntent intent, CopilotDogEvidence evidence)
    {
        if (IsCompatibilityTarget(intent, "Cats", "SmallAnimals"))
        {
            return evidence.DirectEvidence.Any(tag =>
                tag is "Calm near cats" or "Redirectable around cats" or "Needs slow cat introductions");
        }

        if (IsCompatibilityTarget(intent, "Children", "OlderChildren"))
        {
            return HasDirectChildEvidence(evidence);
        }

        if (IsCompatibilityTarget(intent, "SeniorDog", "SensitiveDog", "OtherDogs"))
        {
            return HasAnySeniorDogDirectEvidence(evidence);
        }

        if (IsCompatibilityTarget(intent, "YoungDog"))
        {
            return evidence.DirectEvidence.Any(tag =>
                tag is "Playful dog friends" or "Active owner fit" or "Outdoor play" or "Training games" or "Calm dog company" or "Needs slow dog introductions");
        }

        return evidence.DirectEvidence.Count > 0;
    }

    private static bool HasUsefulCatEvidence(CopilotDogEvidence evidence)
    {
        return evidence.SupportedDisplayTags.Any(tag =>
            tag is "Calm near cats" or "Redirectable around cats" or "Needs slow cat introductions");
    }

    private static bool HasUsefulSeniorDogEvidence(CopilotDogEvidence evidence)
    {
        return HasAnySeniorDogDirectEvidence(evidence);
    }

    private static bool HasDirectChildEvidence(CopilotDogEvidence evidence)
    {
        return evidence.DirectEvidence.Any(tag =>
            tag is "Better with older children" or "Family routine fit");
    }

    private static bool HasStrongDirectSeniorDogEvidence(CopilotDogEvidence evidence)
    {
        return evidence.DirectEvidence.Any(tag =>
            tag is "Calm dog company" or "Respectful around dogs");
    }

    private static bool HasAnySeniorDogDirectEvidence(CopilotDogEvidence evidence)
    {
        return evidence.DirectEvidence.Any(tag =>
            tag is "Calm dog company" or "Respectful around dogs" or "Needs slow dog introductions" or
                "Needs calm dog introductions" or "Gentle play style" or "Not suited to pushy dogs");
    }

    private static bool HasSeniorDogEvidenceCaution(CopilotDogEvidence evidence)
    {
        return evidence.CautionEvidence.Count > 0 ||
            evidence.MissingEvidence.Count > 0 ||
            evidence.SupportedDisplayTags.Any(tag =>
                tag.StartsWith("Ask shelter", StringComparison.OrdinalIgnoreCase) ||
                tag is "Needs slow dog introductions" or "Needs calm dog introductions" or "Not suited to pushy dogs");
    }

    private static bool HasMissingPrimaryCompatibilityEvidence(CopilotDogEvidence evidence)
    {
        return evidence.MissingEvidence.Count > 0 ||
            evidence.SupportedDisplayTags.Any(tag =>
                tag.StartsWith("Ask shelter", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("No ", StringComparison.OrdinalIgnoreCase));
    }

    private static int CalculateIntentEvidenceScore(CopilotIntent intent, CopilotDogEvidence evidence, DogStatus status)
    {
        var score = 0;
        if (intent.PrimaryIntent == "Compatibility")
        {
            if (IsCompatibilityTarget(intent, "SeniorDog", "SensitiveDog"))
            {
                score += Math.Min(26, evidence.DirectEvidence.Count * 10);
                score += Math.Min(10, evidence.IndirectEvidence.Count * 4);
                score += Math.Min(4, evidence.GenericEvidence.Count * 2);
            }
            else
            {
                score += Math.Min(18, evidence.PositiveEvidence.Count * 6);
            }

            score -= Math.Min(18, evidence.NegativeEvidence.Count * 8);
            score -= Math.Min(12, evidence.MissingEvidence.Count * 6);

            if (evidence.SupportedDisplayTags.Any(tag => tag.StartsWith("Ask shelter", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("No ", StringComparison.OrdinalIgnoreCase)))
            {
                score -= 8;
            }
        }
        else if (intent.PrimaryIntent is "HomeSuitability" or "ActivityLevel")
        {
            score += Math.Min(14, evidence.PositiveEvidence.Count * 4);
            score -= Math.Min(12, evidence.NegativeEvidence.Count * 6);
        }
        else
        {
            score += Math.Min(8, evidence.PositiveEvidence.Count * 3);
        }

        if (status == DogStatus.Reserved)
        {
            score -= 1;
        }

        return score;
    }

    private static CopilotDogEvidence ExtractDogEvidence(
        Dog dog,
        AdoptionCopilotSearchDogsArgs args,
        CopilotIntent intent,
        string searchableText,
        IReadOnlyList<string> safeReasons)
    {
        var displayTags = new List<string>();
        var cautionTags = new List<string>();
        var apartmentRequested = IsApartmentRequest(args);
        var yardRequested = IsYardRequest(args);
        var calmRequested = IsCalmRequest(args);
        var strictLongerWalksRequested = HasExplicitLongerWalksRequest(args);
        var dailyWalksRequested = HasExplicitDailyWalksRequest(args);
        var moderateActivityRequested = HasExplicitModerateActivityRequest(args);
        var activityWalksRequested = strictLongerWalksRequested ||
            dailyWalksRequested ||
            moderateActivityRequested ||
            string.Equals(args.ActivityLevel, "Medium", StringComparison.OrdinalIgnoreCase);
        var inferLowActivityFromApartment = apartmentRequested &&
            !activityWalksRequested &&
            !string.Equals(args.ActivityLevel, "High", StringComparison.OrdinalIgnoreCase);
        var activeRequested = HasIntent(args, "active", "playful", "energetic") ||
            string.Equals(args.EnergyLevel, "High", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.ActivityLevel, "High", StringComparison.OrdinalIgnoreCase);
        var beginnerRequested = HasIntent(args, "beginner", "first time", "easy") ||
            string.Equals(args.ExperienceLevel, "Beginner", StringComparison.OrdinalIgnoreCase);
        var childrenRequested = HasChildrenRequest(args);
        var catsRequested = HasCatRequest(args);
        var dogsRequested = HasOtherDogsRequest(args);
        var seniorOrSensitiveDogAtHome = HasSeniorOrSensitiveHouseholdDogRequest(args);
        var youngDogAtHome = HasYoungHouseholdDogRequest(args);

        foreach (var reason in safeReasons)
        {
            var tag = NormalizeDisplayTag(reason, dog);
            if (IsDisplayTagBackedByDogData(tag, dog, searchableText))
            {
                AddDisplayTag(displayTags, tag);
            }
        }

        if ((inferLowActivityFromApartment || calmRequested || seniorOrSensitiveDogAtHome) && ContainsAny(searchableText, ["short daily walks", "short walks", "slow walks", "leash walks"]))
        {
            AddDisplayTag(displayTags, "Short walks");
        }

        if ((inferLowActivityFromApartment || calmRequested || seniorOrSensitiveDogAtHome) && ContainsAny(searchableText, ["indoor rest", "quiet evenings indoors", "resting close to people"]))
        {
            AddDisplayTag(displayTags, "Indoor rest");
        }

        if ((inferLowActivityFromApartment || calmRequested || seniorOrSensitiveDogAtHome || activityWalksRequested) && ContainsAny(searchableText, ["settles down quickly", "settles quickly", "settles", "settle"]))
        {
            AddDisplayTag(displayTags, "Settles quickly");
        }

        if ((inferLowActivityFromApartment || calmRequested || seniorOrSensitiveDogAtHome) && ContainsAny(searchableText, ["quiet routine", "predictable routine", "daily rhythm is predictable", "predictable habits", "relaxed evenings"]))
        {
            AddDisplayTag(displayTags, "Quiet routine");
        }

        if (apartmentRequested && dog.Size == DogSize.Small)
        {
            AddDisplayTag(displayTags, "Small size");
        }
        else if (apartmentRequested && dog.Size == DogSize.Medium)
        {
            AddDisplayTag(displayTags, "Medium size");
        }

        if ((activeRequested || yardRequested || youngDogAtHome || strictLongerWalksRequested) && HasLongerWalkEvidence(searchableText))
        {
            AddDisplayTag(displayTags, "Longer walks");
        }

        if (dailyWalksRequested && HasDailyWalkEvidence(searchableText))
        {
            AddDisplayTag(displayTags, "Daily walks");
        }

        if ((activeRequested || yardRequested || youngDogAtHome) && ContainsAny(searchableText, ["outdoor play", "space to run", "open areas", "room to run", "stretch his legs"]))
        {
            AddDisplayTag(displayTags, "Outdoor play");
        }

        if ((activeRequested || yardRequested || youngDogAtHome) && ContainsAny(searchableText, ["training games", "fetch"]))
        {
            AddDisplayTag(displayTags, "Training games");
        }

        if ((activeRequested || yardRequested || youngDogAtHome) && HasPositiveActiveEvidence(searchableText))
        {
            AddDisplayTag(displayTags, "Active owner fit");
        }

        if (beginnerRequested && ContainsAny(searchableText, ["gentle handling", "takes guidance easily", "responds well to routine", "easy to redirect", "predictable"]))
        {
            AddDisplayTag(displayTags, "Beginner-suitable routine");
        }

        if ((beginnerRequested || childrenRequested || seniorOrSensitiveDogAtHome) && ContainsAny(searchableText, ["gentle handling", "takes treats gently", "positive handling"]))
        {
            AddDisplayTag(displayTags, "Gentle handling");
        }

        if (childrenRequested && ContainsAny(searchableText, ["older children during supervised visits", "supervised visits with older children"]))
        {
            AddDisplayTag(displayTags, "Better with older children");
        }

        if (childrenRequested && ContainsAny(searchableText, ["family setting", "family routine", "predictable family routines", "calm family setting"]))
        {
            AddDisplayTag(displayTags, "Family routine fit");
        }

        if (catsRequested && ContainsAny(searchableText, ["passed the shelter cats calmly", "passed calmly by the shelter cats", "passed shelter cats calmly"]))
        {
            AddDisplayTag(displayTags, "Calm near cats");
        }

        if (catsRequested && ContainsAny(searchableText, ["notices cats", "redirected with treats", "slow introductions to cats", "slow introductions to smaller animals"]))
        {
            if (ContainsAny(searchableText, ["notices cats", "redirected with treats"]))
            {
                AddDisplayTag(displayTags, "Redirectable around cats");
            }

            AddDisplayTag(displayTags, "Needs slow cat introductions");
        }

        if (dogsRequested && ContainsAny(searchableText,
            [
                "walks politely beside familiar calm dogs",
                "comfortable with calm dogs",
                "calm dog company",
                "familiar calm dogs",
                "more comfortable with calm dogs",
                "calm dogs are easier"
            ]))
        {
            AddDisplayTag(displayTags, "Calm dog company");
            AddDisplayTag(displayTags, "Respectful around dogs");
        }

        if (dogsRequested && ContainsAny(searchableText, ["enjoys sturdy, playful dogs", "playful dogs after", "play sessions with steady dogs"]))
        {
            AddDisplayTag(displayTags, "Playful dog friends");
        }

        if (dogsRequested && ContainsAny(searchableText,
            [
                "slow introductions",
                "steady dogs over bouncy playmates",
                "prefers steady dogs",
                "more comfortable with calm dogs",
                "calm dogs are easier",
                "introductions should stay calm"
            ]))
        {
            AddDisplayTag(displayTags, "Needs slow dog introductions");
            if (seniorOrSensitiveDogAtHome)
            {
                AddDisplayTag(displayTags, "Needs calm dog introductions");
            }
        }

        if (seniorOrSensitiveDogAtHome && HasDogToDogGentlePlayEvidence(searchableText))
        {
            AddDisplayTag(displayTags, "Gentle play style");
        }

        if (seniorOrSensitiveDogAtHome && ContainsAny(searchableText, ["pushy dogs can make", "calm dogs are easier", "pushy playmates"]))
        {
            AddDisplayTag(displayTags, "Not suited to pushy dogs");
        }

        if (seniorOrSensitiveDogAtHome &&
            (HasCalmSignal(searchableText) && !HasActiveSignal(searchableText) ||
            HasCalmDogPreferenceSignal(searchableText)))
        {
            AddDisplayTag(displayTags, "Not too energetic");
        }

        if (dog.Status == DogStatus.Reserved)
        {
            AddDisplayTag(cautionTags, "Reserved - availability may change");
        }

        if (catsRequested && ContainsAny(searchableText, ["fast-moving cats", "fast-moving small animals", "quick cats", "small animals are likely too exciting", "too interested in fast-moving small animals"]))
        {
            AddDisplayTag(cautionTags, "Not suitable with cats");
        }

        if (childrenRequested && ContainsAny(searchableText, ["very noisy young children", "very young children", "noisy, chaotic play"]))
        {
            AddDisplayTag(cautionTags, "Not ideal for young children");
        }

        if ((beginnerRequested || calmRequested || apartmentRequested || seniorOrSensitiveDogAtHome) && ContainsAny(searchableText, ["watches new people carefully", "patient adopter", "quiet encouragement", "needs slow introductions"]))
        {
            AddDisplayTag(cautionTags, "Patient adopter needed");
        }

        if ((inferLowActivityFromApartment || calmRequested || seniorOrSensitiveDogAtHome) && HasActiveSignal(searchableText) && !HasCalmSignal(searchableText))
        {
            AddDisplayTag(cautionTags, "Very energetic");
        }

        if ((inferLowActivityFromApartment || calmRequested) && HasHigherActivityNeedSignal(searchableText))
        {
            AddDisplayTag(cautionTags, "Higher activity needs");
        }

        if (strictLongerWalksRequested && HasShortWalkEvidence(searchableText) && !HasLongerWalkEvidence(searchableText))
        {
            AddDisplayTag(cautionTags, "Prefers shorter walks");
        }

        var hasApartmentSpaceConflict = ContainsAny(searchableText, ["space to run", "room to run", "open areas", "requires a yard", "requires yard"]);
        if (apartmentRequested && hasApartmentSpaceConflict)
        {
            AddDisplayTag(cautionTags, "Needs more space");
        }

        if (apartmentRequested &&
            dog.Size == DogSize.Large &&
            !HasExplicitApartmentFitEvidence(searchableText))
        {
            AddDisplayTag(cautionTags, "Large dog - confirm apartment fit");
        }

        if (apartmentRequested &&
            !HasExplicitApartmentFitEvidence(searchableText) &&
            (!activityWalksRequested || dog.Size == DogSize.Large || hasApartmentSpaceConflict))
        {
            AddDisplayTag(cautionTags, "Ask shelter about apartment fit");
        }

        if (seniorOrSensitiveDogAtHome && HasSeniorDogOverwhelmRisk(searchableText))
        {
            AddDisplayTag(cautionTags, HasCompatibility(args, "SeniorDog")
                ? "May overwhelm senior dog"
                : "May overwhelm sensitive dogs");
        }

        if (seniorOrSensitiveDogAtHome && ContainsAny(searchableText, ["only dog", "only pet", "prefers being the only dog"]))
        {
            AddDisplayTag(cautionTags, "May prefer only dog");
        }

        if (HasSparseProfileText(searchableText))
        {
            AddDisplayTag(cautionTags, "Missing behavior details");
        }

        var display = displayTags
            .Where(tag => IsDisplayTagRelevantToIntent(tag, args, intent))
            .Take(4)
            .ToList();
        if (catsRequested && !display.Any(tag => tag.Contains("cat", StringComparison.OrdinalIgnoreCase)))
        {
            AddDisplayTag(display, "Ask shelter about cats");
        }

        if (seniorOrSensitiveDogAtHome && !HasDirectSeniorDogDisplayEvidence(display))
        {
            AddDisplayTag(display, HasCompatibility(args, "SeniorDog")
                ? "Ask shelter about senior dog fit"
                : "Ask shelter about sensitive dog fit");
        }

        if (childrenRequested && !HasDirectChildDisplayEvidence(display))
        {
            AddDisplayTag(display, "Ask shelter about children");
        }

        var caution = cautionTags.Take(3).ToList();
        var missing = BuildMissingEvidence(intent, display, caution);
        var direct = ClassifyDirectEvidence(intent, display);
        var indirect = ClassifyIndirectEvidence(intent, display);
        var generic = ClassifyGenericEvidence(intent, searchableText, display, direct);
        var negative = caution
            .Where(tag => tag is not "Reserved - availability may change" and not "Patient adopter needed" and not "Missing behavior details" and not "Large dog - confirm apartment fit")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var positive = display
            .Where(tag => !tag.StartsWith("Ask shelter", StringComparison.OrdinalIgnoreCase) &&
                !tag.StartsWith("No ", StringComparison.OrdinalIgnoreCase) &&
                !negative.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var positiveItems = BuildEvidenceItems(dog, direct, "Direct")
            .Concat(BuildEvidenceItems(dog, indirect, "Indirect"))
            .Concat(BuildEvidenceItems(dog, generic, "Generic"))
            .DistinctBy(item => $"{item.Strength}:{item.Label}", StringComparer.OrdinalIgnoreCase)
            .ToList();
        var cautionItems = BuildEvidenceItems(dog, caution, "Caution");
        var negativeItems = BuildEvidenceItems(dog, negative, "Caution");
        var missingItems = missing
            .Select(label => new EvidenceItem(label, "Missing", "MissingEvidence", label))
            .ToList();
        var summary = string.Join("; ", display.Concat(caution).Take(5));
        return new CopilotDogEvidence(
            dog.Id,
            direct,
            indirect,
            generic,
            positive,
            caution,
            negative,
            missing,
            positiveItems,
            cautionItems,
            negativeItems,
            missingItems,
            display,
            summary);
    }

    private static IReadOnlyList<EvidenceItem> BuildEvidenceItems(
        Dog dog,
        IEnumerable<string> labels,
        string strength)
    {
        return labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => CreateEvidenceItem(dog, label.Trim(), strength))
            .ToList();
    }

    private static EvidenceItem CreateEvidenceItem(Dog dog, string label, string strength)
    {
        var sourceField = InferEvidenceSourceField(dog, label);
        var matchedText = sourceField switch
        {
            "Size" => dog.Size.ToString(),
            "Status" => dog.Status.ToString(),
            "Shelter" => string.Join(", ", new[] { dog.Shelter?.Name, dog.Shelter?.Neighborhood, dog.Shelter?.City }
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            "Description" => FindEvidenceSentence(dog.Description, label),
            "BehaviorDescription" => FindEvidenceSentence(dog.BehaviorDescription, label),
            _ => label
        };

        return new EvidenceItem(label, strength, sourceField, EmptyToNull(matchedText) ?? label);
    }

    private static string InferEvidenceSourceField(Dog dog, string label)
    {
        if (label.Contains("size", StringComparison.OrdinalIgnoreCase))
        {
            return "Size";
        }

        if (label.Contains("Reserved", StringComparison.OrdinalIgnoreCase))
        {
            return "Status";
        }

        if (label.Contains("shelter", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("area", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("neighborhood", StringComparison.OrdinalIgnoreCase))
        {
            return "Shelter";
        }

        var terms = GetEvidenceTerms(label);
        if (!string.IsNullOrWhiteSpace(dog.BehaviorDescription) &&
            terms.Any(term => dog.BehaviorDescription.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return "BehaviorDescription";
        }

        if (!string.IsNullOrWhiteSpace(dog.Description) &&
            terms.Any(term => dog.Description.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return "Description";
        }

        return !string.IsNullOrWhiteSpace(dog.BehaviorDescription)
            ? "BehaviorDescription"
            : "Description";
    }

    private static string? FindEvidenceSentence(string? text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var terms = GetEvidenceTerms(label);
        var sentence = Regex.Split(text.Trim(), @"(?<=[.!?])\s+")
            .FirstOrDefault(part => terms.Any(term => part.Contains(term, StringComparison.OrdinalIgnoreCase)));

        return EmptyToNull(sentence) ?? text.Trim();
    }

    private static IReadOnlyList<string> GetEvidenceTerms(string label)
    {
        return label switch
        {
            "Calm dog company" => ["calm dogs", "familiar calm dogs", "calm dog company", "comfortable with calm dogs"],
            "Respectful around dogs" => ["walks politely", "respectful", "familiar calm dogs", "comfortable with calm dogs"],
            "Needs slow dog introductions" or "Needs calm dog introductions" => ["slow introductions", "introductions should stay calm", "proper introduction"],
            "Gentle play style" => ["gentle play", "plays gently", "steady dogs"],
            "Not suited to pushy dogs" => ["pushy dogs", "pushy playmates"],
            "Not too energetic" => ["quiet routine", "settles", "calm", "short walks", "indoor rest"],
            "Calm near cats" => ["shelter cats", "cats calmly"],
            "Redirectable around cats" => ["redirected", "notices cats"],
            "Needs slow cat introductions" => ["slow introductions to cats", "slow introductions to smaller animals", "notices cats"],
            "Not suitable with cats" => ["fast-moving cats", "fast-moving small animals", "too interested"],
            "Better with older children" => ["older children", "supervised visits"],
            "Family routine fit" => ["family routine", "family setting"],
            "Gentle handling" => ["gentle handling", "takes treats gently", "positive handling", "gentle"],
            "Short walks" => ["short walks", "short daily walks", "slow walks", "leash walks"],
            "Indoor rest" => ["indoor rest", "quiet evenings indoors", "resting close"],
            "Settles quickly" => ["settles", "settle"],
            "Quiet routine" => ["quiet routine", "predictable routine", "relaxed evenings", "predictable"],
            "Outdoor play" => ["outdoor play", "space to run", "open areas", "room to run"],
            "Daily walks" => ["daily walks", "regular walks"],
            "Longer walks" => ["longer walks", "long walks", "active walks", "brisk walks", "medium walk", "structured walks", "hiking"],
            "Training games" => ["training games", "fetch"],
            "Active owner fit" => ["active", "playful", "energetic", "running"],
            "Reserved - availability may change" => ["reserved"],
            "Patient adopter needed" => ["patient adopter", "quiet encouragement", "needs slow introductions"],
            "Very energetic" => ["very energetic", "active", "running", "space to run"],
            "Higher activity needs" => ["longer walks", "use his energy", "regular outdoor time", "medium walk", "exercise needs"],
            "Prefers shorter walks" => ["short walks", "short daily walks", "slow walks"],
            "Ask shelter about apartment fit" => ["apartment", "smaller home", "smaller spaces", "flat"],
            "Needs more space" => ["space to run", "room to run", "open areas", "outdoor play"],
            "Missing behavior details" => ["missing behavior details"],
            _ => label.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };
    }

    private static bool HasDirectSeniorDogDisplayEvidence(IReadOnlyList<string> displayTags)
    {
        return displayTags.Any(tag =>
            tag is "Calm dog company" or "Respectful around dogs" or "Needs slow dog introductions" or
                "Needs calm dog introductions" or "Gentle play style" or "Not suited to pushy dogs");
    }

    private static bool HasDirectChildDisplayEvidence(IReadOnlyList<string> displayTags)
    {
        return displayTags.Any(tag => tag is "Better with older children" or "Family routine fit");
    }

    private static bool HasDogToDogGentlePlayEvidence(string searchableText)
    {
        return ContainsAny(searchableText,
            [
                "gentle play with other dogs",
                "gentle play style",
                "plays gently with dogs",
                "play sessions with steady dogs",
                "steady dogs",
                "prefers steady dogs"
            ]);
    }

    private static IReadOnlyList<string> ClassifyDirectEvidence(CopilotIntent intent, IReadOnlyList<string> displayTags)
    {
        if (IsCompatibilityTarget(intent, "SeniorDog", "SensitiveDog"))
        {
            return displayTags
                .Where(tag => tag is "Calm dog company" or "Respectful around dogs" or "Needs slow dog introductions" or
                    "Needs calm dog introductions" or "Gentle play style" or "Not suited to pushy dogs")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (IsCompatibilityTarget(intent, "Cats", "SmallAnimals"))
        {
            return displayTags
                .Where(tag => tag is "Calm near cats" or "Redirectable around cats" or "Needs slow cat introductions")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (IsCompatibilityTarget(intent, "Children", "OlderChildren"))
        {
            return displayTags
                .Where(tag => tag is "Better with older children" or "Family routine fit")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (intent.PrimaryIntent is "HomeSuitability" or "ActivityLevel")
        {
            return displayTags
                .Where(tag => tag is "Short walks" or "Indoor rest" or "Settles quickly" or "Quiet routine" or
                    "Small size" or "Medium size" or "Daily walks" or "Longer walks" or "Outdoor play" or "Training games" or "Active owner fit")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return [];
    }

    private static IReadOnlyList<string> ClassifyIndirectEvidence(CopilotIntent intent, IReadOnlyList<string> displayTags)
    {
        if (IsCompatibilityTarget(intent, "SeniorDog", "SensitiveDog"))
        {
            return displayTags
                .Where(tag => tag is "Not too energetic" or "Gentle handling" or "Short walks" or "Indoor rest" or
                    "Settles quickly" or "Quiet routine")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (IsCompatibilityTarget(intent, "Children", "OlderChildren"))
        {
            return displayTags
                .Where(tag => tag is "Gentle handling" or "Needs calm children")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return [];
    }

    private static IReadOnlyList<string> ClassifyGenericEvidence(
        CopilotIntent intent,
        string searchableText,
        IReadOnlyList<string> displayTags,
        IReadOnlyList<string> directEvidence)
    {
        if (intent.PrimaryIntent != "Compatibility" || directEvidence.Count > 0)
        {
            return [];
        }

        var generic = new List<string>();
        if (ContainsAny(searchableText, ["friendly", "sweet", "affectionate", "likes people", "good dog", "good with people"]))
        {
            generic.Add("generic friendly wording");
        }

        if (displayTags.Any(tag => tag is "Gentle handling" or "Not too energetic"))
        {
            generic.Add("generic calm wording");
        }

        return generic.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> BuildMissingEvidence(
        CopilotIntent intent,
        IReadOnlyList<string> displayTags,
        IReadOnlyList<string> cautionTags)
    {
        if (intent.PrimaryIntent != "Compatibility")
        {
            return [];
        }

        if (IsCompatibilityTarget(intent, "Cats", "SmallAnimals") &&
            !displayTags.Any(tag => tag is "Calm near cats" or "Redirectable around cats" or "Needs slow cat introductions"))
        {
            return ["no cat evidence"];
        }

        if (IsCompatibilityTarget(intent, "Children", "OlderChildren") &&
            !displayTags.Any(tag => tag is "Better with older children" or "Family routine fit" ||
                tag.Contains("Family", StringComparison.OrdinalIgnoreCase)))
        {
            return ["no child evidence"];
        }

        if (IsCompatibilityTarget(intent, "SeniorDog", "SensitiveDog", "OtherDogs") &&
            !displayTags.Any(tag => tag is "Calm dog company" or "Respectful around dogs" or "Needs slow dog introductions" ||
                tag is "Needs calm dog introductions" or "Gentle play style" or "Not suited to pushy dogs") &&
            !cautionTags.Any(tag => tag.Contains("dog", StringComparison.OrdinalIgnoreCase)))
        {
            return ["no dog compatibility evidence"];
        }

        return [];
    }

    private static bool IsDisplayTagRelevantToIntent(string tag, AdoptionCopilotSearchDogsArgs args, CopilotIntent intent)
    {
        var apartmentRequested = IsApartmentRequest(args);
        var yardRequested = IsYardRequest(args);
        var calmRequested = IsCalmRequest(args);
        var strictLongerWalksRequested = HasExplicitLongerWalksRequest(args);
        var dailyWalksRequested = HasExplicitDailyWalksRequest(args);
        var moderateActivityRequested = HasExplicitModerateActivityRequest(args);
        var activityWalksRequested = strictLongerWalksRequested ||
            dailyWalksRequested ||
            moderateActivityRequested ||
            string.Equals(args.ActivityLevel, "Medium", StringComparison.OrdinalIgnoreCase);
        var allowLowApartmentTags = apartmentRequested &&
            !activityWalksRequested &&
            !string.Equals(args.ActivityLevel, "High", StringComparison.OrdinalIgnoreCase);
        var activeRequested = HasIntent(args, "active", "playful", "energetic") ||
            string.Equals(args.EnergyLevel, "High", StringComparison.OrdinalIgnoreCase);
        var beginnerRequested = HasIntent(args, "beginner", "first time", "easy") ||
            string.Equals(args.ExperienceLevel, "Beginner", StringComparison.OrdinalIgnoreCase);
        var childrenRequested = HasChildrenRequest(args);
        var catsRequested = HasCatRequest(args);
        var dogsRequested = HasOtherDogsRequest(args);
        var seniorOrSensitiveDogAtHome = HasSeniorOrSensitiveHouseholdDogRequest(args);
        var youngDogAtHome = HasYoungHouseholdDogRequest(args);
        var sizeRequested = args.Sizes?.Count > 0;
        var primaryIntent = intent.PrimaryIntent;
        var allowLifestyleTagsForCompatibility = allowLowApartmentTags || HasExplicitLowActivityRequest(args);
        var allowActiveTagsForCompatibility = yardRequested || activeRequested;
        var allowDogTagsForCompatibility = dogsRequested || seniorOrSensitiveDogAtHome || youngDogAtHome;
        if (primaryIntent == "Compatibility")
        {
            if (catsRequested)
            {
                if (tag is "Calm near cats" or "Redirectable around cats" or "Needs slow cat introductions" or "Not suitable with cats" or "Ask shelter about cats" or "No cat history found")
                {
                    return true;
                }

                if (!allowLifestyleTagsForCompatibility && !allowDogTagsForCompatibility)
                {
                    return false;
                }
            }

            if (seniorOrSensitiveDogAtHome)
            {
                if (tag is "Calm dog company" or "Respectful around dogs" or "Needs slow dog introductions" or "Needs calm dog introductions" or "Gentle play style" or "Not too energetic" or "Not suited to pushy dogs" or "May overwhelm senior dog" or "May overwhelm sensitive dogs" or "May prefer only dog" or "Ask shelter about senior dog fit" or "Ask shelter about sensitive dog fit" or "No dog compatibility history found")
                {
                    return true;
                }

                if (!allowLifestyleTagsForCompatibility)
                {
                    return false;
                }
            }

            if (childrenRequested)
            {
                if (tag is "Better with older children" or "Gentle handling" or "Family routine fit" or "Needs calm children" or "Not ideal for young children" or "Ask shelter about children")
                {
                    return true;
                }

                if (!allowLifestyleTagsForCompatibility && !allowDogTagsForCompatibility)
                {
                    return false;
                }
            }

            if (dogsRequested || youngDogAtHome)
            {
                if (tag is "Calm dog company" or "Respectful around dogs" or "Playful dog friends" or "Needs slow dog introductions" or "Needs calm dog introductions" or "Gentle play style" or "Not suited to pushy dogs" or "Ask shelter about senior dog fit" or "Ask shelter about sensitive dog fit" or "No dog compatibility history found")
                {
                    return true;
                }

                if (!allowLifestyleTagsForCompatibility && !allowActiveTagsForCompatibility)
                {
                    return false;
                }
            }
        }

        var hasSpecificIntent = apartmentRequested ||
            yardRequested ||
            calmRequested ||
            activeRequested ||
            beginnerRequested ||
            childrenRequested ||
            catsRequested ||
            dogsRequested ||
            seniorOrSensitiveDogAtHome ||
            youngDogAtHome ||
            sizeRequested;

        if (!hasSpecificIntent)
        {
            return true;
        }

        return tag switch
        {
            "Short walks" or "Indoor rest" or "Quiet routine" or "Lower activity fit" => allowLowApartmentTags || calmRequested,
            "Settles quickly" => allowLowApartmentTags || calmRequested || activityWalksRequested,
            "Small size" or "Medium size" => apartmentRequested || sizeRequested,
            "Daily walks" => dailyWalksRequested,
            "Longer walks" => yardRequested || activeRequested || youngDogAtHome || strictLongerWalksRequested,
            "Outdoor play" or "Training games" or "Active owner fit" or "Space to run" => yardRequested || activeRequested || youngDogAtHome,
            "Gentle handling" => beginnerRequested || childrenRequested || seniorOrSensitiveDogAtHome,
            "Beginner-suitable routine" or "Patient adopter needed" => beginnerRequested,
            "Family routine fit" or "Better with older children" or "Needs calm children" => childrenRequested,
            "Calm near cats" or "Redirectable around cats" or "Needs slow cat introductions" => catsRequested,
            "Calm dog company" or "Respectful around dogs" or "Needs slow dog introductions" or "Needs calm dog introductions" or "Gentle play style" or "Not too energetic" or "Not suited to pushy dogs" => dogsRequested || seniorOrSensitiveDogAtHome || youngDogAtHome,
            "Playful dog friends" => dogsRequested || youngDogAtHome,
            "Prefers shorter walks" => strictLongerWalksRequested,
            _ => false
        };
    }

    private static void AddDisplayTag(List<string> tags, string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag) ||
            !AllowedDisplayTags.Contains(tag) ||
            tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        tags.Add(tag);
    }

    private static string? NormalizeDisplayTag(string reason, Dog dog)
    {
        var lower = reason.ToLowerInvariant();
        if (lower.Contains("short walk") || lower.Contains("slow walk"))
        {
            return "Short walks";
        }

        if (lower.Contains("daily walk") || lower.Contains("regular walk"))
        {
            return "Daily walks";
        }

        if (lower.Contains("indoor"))
        {
            return "Indoor rest";
        }

        if (lower.Contains("settles"))
        {
            return "Settles quickly";
        }

        if (lower.Contains("quiet") || lower.Contains("predictable"))
        {
            return "Quiet routine";
        }

        if (lower.Contains("small size"))
        {
            return "Small size";
        }

        if (lower.Contains("medium size"))
        {
            return "Medium size";
        }

        if (lower.Contains("longer walk") || lower.Contains("long walk") || lower.Contains("active walk") || lower.Contains("brisk walk") || lower.Contains("hiking"))
        {
            return "Longer walks";
        }

        if (lower.Contains("outdoor") || lower.Contains("yard"))
        {
            return "Outdoor play";
        }

        if (lower.Contains("training games") || lower.Contains("fetch"))
        {
            return "Training games";
        }

        if (lower.Contains("active"))
        {
            return "Active owner fit";
        }

        if (lower.Contains("gentle handling"))
        {
            return "Gentle handling";
        }

        if (lower.Contains("beginner") || lower.Contains("first-dog"))
        {
            return "Beginner-suitable routine";
        }

        if (lower.Contains("older") && lower.Contains("children"))
        {
            return "Better with older children";
        }

        if (lower.Contains("family"))
        {
            return "Family routine fit";
        }

        if (lower.Contains("calm near cats") || lower.Contains("shelter cats"))
        {
            return "Calm near cats";
        }

        if (lower.Contains("slow cat"))
        {
            return "Needs slow cat introductions";
        }

        if (lower.Contains("calm dog company"))
        {
            return "Calm dog company";
        }

        if (lower.Contains("dog company") || lower.Contains("playful dog"))
        {
            return "Playful dog friends";
        }

        if (lower.Contains("slow dog"))
        {
            return "Needs slow dog introductions";
        }

        if (lower.Contains("reserved"))
        {
            return "Reserved - availability may change";
        }

        return dog.Size switch
        {
            DogSize.Small when lower.Contains("size") => "Small size",
            DogSize.Medium when lower.Contains("size") => "Medium size",
            _ => null
        };
    }

    private static bool IsDisplayTagBackedByDogData(string? tag, Dog dog, string searchableText)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        if (tag == "Small size")
        {
            return dog.Size == DogSize.Small;
        }

        if (tag == "Medium size")
        {
            return dog.Size == DogSize.Medium;
        }

        if (tag == "Reserved - availability may change")
        {
            return dog.Status == DogStatus.Reserved;
        }

        if (tag == "Short walks" || tag == "Prefers shorter walks")
        {
            return HasShortWalkEvidence(searchableText);
        }

        if (tag == "Daily walks")
        {
            return HasDailyWalkEvidence(searchableText);
        }

        if (tag == "Longer walks")
        {
            return HasLongerWalkEvidence(searchableText);
        }

        return GetEvidenceTerms(tag).Any(term =>
            searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddLifestyleScores(
        Dog dog,
        AdoptionCopilotSearchDogsArgs args,
        string searchableText,
        List<string> reasons,
        ref int score)
    {
        // Lifestyle requests are soft ranking signals. Hard safety filters stay in MatchesHardFilters.
        var apartmentRequested = IsApartmentRequest(args);
        var yardRequested = IsYardRequest(args);
        var calmRequested = HasIntent(args, "calm", "quiet", "gentle") ||
            string.Equals(args.EnergyLevel, "Low", StringComparison.OrdinalIgnoreCase);
        var friendlyRequested = HasIntent(args, "friendly", "social", "affectionate");
        var strictLongerWalksRequested = HasExplicitLongerWalksRequest(args);
        var dailyWalksRequested = HasExplicitDailyWalksRequest(args);
        var moderateActivityRequested = HasExplicitModerateActivityRequest(args);
        var activityWalksRequested = strictLongerWalksRequested ||
            dailyWalksRequested ||
            moderateActivityRequested ||
            string.Equals(args.ActivityLevel, "Medium", StringComparison.OrdinalIgnoreCase);
        var inferLowActivityFromApartment = apartmentRequested &&
            !activityWalksRequested &&
            !string.Equals(args.ActivityLevel, "High", StringComparison.OrdinalIgnoreCase);
        var activeRequested = HasIntent(args, "active", "playful", "energetic") ||
            string.Equals(args.EnergyLevel, "High", StringComparison.OrdinalIgnoreCase);
        var beginnerRequested = HasIntent(args, "beginner", "first time", "easy");
        var childrenRequested = HasChildrenRequest(args);
        var petsRequested = args.GoodWithPets == true || HasCatRequest(args) || HasOtherDogsRequest(args) || HasIntent(args, "pets");

        if (inferLowActivityFromApartment || calmRequested)
        {
            if (ContainsAny(searchableText, ["short daily walks", "short walks", "slow walks", "leash walks"]))
            {
                AddReason(reasons, "Short walks");
                score += 10;
            }

            if (ContainsAny(searchableText, ["indoor rest", "smaller spaces", "indoor", "relaxed evenings"]))
            {
                AddReason(reasons, "Indoor rest");
                score += 8;
            }

            if (ContainsAny(searchableText, ["settles down quickly", "settles quickly", "settles", "settle"]))
            {
                AddReason(reasons, "Settles quickly");
                score += 8;
            }

            if (ContainsAny(searchableText, ["quiet routine", "predictable environment", "predictable routine", "quiet handling", "relaxed"]))
            {
                AddReason(reasons, "Quiet routine");
                score += 7;
            }

            if (ContainsAny(searchableText, CalmSignals))
            {
                AddReason(reasons, calmRequested ? "Relaxed temperament" : "Lower activity fit");
                score += calmRequested ? 9 : 7;
            }

            if (apartmentRequested && ContainsAny(searchableText, ["apartment", "flat", "smaller spaces", "indoor rest", "short walks", "short daily walks", "leash walks"]))
            {
                AddReason(reasons, "Apartment lifestyle fit");
                score += 7;
            }

            if (dog.Size == DogSize.Large && !ContainsAny(searchableText, ["apartment", "short walks", "indoor rest", "calm", "quiet", "low energy"]))
            {
                score -= 8;
            }
        }

        if (strictLongerWalksRequested)
        {
            if (HasLongerWalkEvidence(searchableText))
            {
                AddReason(reasons, "Enjoys longer walks");
                score += 11;
            }
            else if (HasShortWalkEvidence(searchableText) ||
                ContainsAny(searchableText, ["low energy", "does not need long outdoor sessions"]))
            {
                score -= 7;
            }
        }
        else if (dailyWalksRequested && (HasDailyWalkEvidence(searchableText) || HasShortWalkEvidence(searchableText)))
        {
            AddReason(reasons, "Daily walks");
            score += 8;
        }
        else if (moderateActivityRequested && (HasLongerWalkEvidence(searchableText) || HasDailyWalkEvidence(searchableText)))
        {
            AddReason(reasons, HasLongerWalkEvidence(searchableText) ? "Enjoys longer walks" : "Daily walks");
            score += 8;
        }

        if (friendlyRequested && ContainsAny(searchableText, ["friendly", "social", "affectionate", "good with people", "good with children", "good with dogs"]))
        {
            AddReason(reasons, "Friendly temperament");
            score += 10;
        }

        if ((activeRequested || activityWalksRequested) && HasPositiveActiveEvidence(searchableText))
        {
            if (HasLongerWalkEvidence(searchableText))
            {
                AddReason(reasons, "Enjoys longer walks");
                score += 8;
            }

            if (ContainsAny(searchableText, ["outdoor play", "fetch", "training games", "open areas", "space to run", "yard", "garden"]))
            {
                AddReason(reasons, yardRequested ? "Yard fit" : "Outdoor play");
                score += 9;
            }

            if (ContainsAny(searchableText, ["active", "playful", "energetic", "running", "space to run"]))
            {
                AddReason(reasons, "Active lifestyle fit");
                score += 8;
            }
        }

        if (beginnerRequested && HasBeginnerEvidence(searchableText))
        {
            if (ContainsAny(searchableText, ["predictable", "routine", "gentle", "calm"]))
            {
                AddReason(reasons, "Predictable routine");
                score += 7;
            }

            AddReason(reasons, "Easier first-dog fit");
            score += 5;
        }

        if (childrenRequested && HasPositiveChildrenEvidence(args, searchableText))
        {
            AddReason(reasons, "Good with children");
            score += 8;
        }

        if (petsRequested)
        {
            AddPetEvidenceScores(args, searchableText, reasons, ref score);
        }

        AddHouseholdDogContextScores(args, searchableText, reasons, ref score);

        if (yardRequested && HasPositiveYardEvidence(searchableText))
        {
            AddReason(reasons, "Yard fit");
            score += 7;
        }
    }

    private static void AddHouseholdDogContextScores(
        AdoptionCopilotSearchDogsArgs args,
        string searchableText,
        List<string> reasons,
        ref int score)
    {
        if (HasSeniorOrSensitiveHouseholdDogRequest(args))
        {
            if (ContainsAny(searchableText,
                [
                    "walks politely beside familiar calm dogs",
                    "comfortable with calm dogs",
                    "calm dog company",
                    "familiar calm dogs",
                    "more comfortable with calm dogs",
                    "calm dogs are easier"
                ]))
            {
                AddReason(reasons, "Calm dog company");
                score += 16;
            }

            if (ContainsAny(searchableText,
                [
                    "slow introductions",
                    "steady dogs over bouncy playmates",
                    "prefers steady dogs",
                    "more comfortable with calm dogs",
                    "calm dogs are easier",
                    "introductions should stay calm",
                    "pushy dogs can make her retreat"
                ]))
            {
                AddReason(reasons, "Slow dog introductions");
                score += 10;
            }

            if (ContainsAny(searchableText, ["short walks", "slow walks", "quiet routine", "predictable routine", "gentle handling", "settles"]))
            {
                AddReason(reasons, "Quiet routine");
                score += 8;
            }

            if (HasActiveSignal(searchableText) && !HasCalmSignal(searchableText))
            {
                score -= 12;
            }
        }

        if (HasYoungHouseholdDogRequest(args))
        {
            if (ContainsAny(searchableText, ["enjoys sturdy, playful dogs", "playful dogs after", "play sessions with steady dogs"]))
            {
                AddReason(reasons, "Playful dog friends");
                score += 16;
            }

            if (HasPositiveActiveEvidence(searchableText))
            {
                AddReason(reasons, "Active owner fit");
                score += 6;
            }

            if (ContainsAny(searchableText, ["slow introductions", "respects pauses", "proper introduction"]))
            {
                AddReason(reasons, "Slow dog introductions");
                score += 6;
            }
        }
    }

    private static bool HasPositiveActiveEvidence(string searchableText)
    {
        return ContainsAny(searchableText,
            [
                "active dog",
                "active family",
                "active lifestyle",
                "playful",
                "energetic dog",
                "smart, energetic",
                "running",
                "space to run",
                "longer walks",
                "brisk walks",
                "hiking",
                "outdoor play",
                "fetch",
                "training games",
                "open areas"
            ]);
    }

    private static bool HasPositiveYardEvidence(string searchableText)
    {
        return ContainsAny(searchableText,
            [
                "yard",
                "garden",
                "outdoor play",
                "space to run",
                "open areas",
                "fetch",
                "training games"
            ]);
    }

    private static bool HasBeginnerEvidence(string searchableText)
    {
        return ContainsAny(searchableText,
            [
                "beginner",
                "first time",
                "easy dog",
                "gentle handling",
                "predictable routine",
                "predictable environment",
                "quiet routine",
                "settles",
                "calm",
                "gentle"
            ]);
    }

    private static bool HasPositiveChildrenEvidence(AdoptionCopilotSearchDogsArgs args, string searchableText)
    {
        var childrenRequested = HasIntent(args, "children", "kids");
        if (childrenRequested)
        {
            return ContainsAny(searchableText,
                [
                    "good with children",
                    "good with kids",
                    "around older children",
                    "around children",
                    "around kids",
                    "comfortable with children",
                    "comfortable with kids",
                    "older children during supervised visits"
                ]);
        }

        return HasIntent(args, "family") &&
            ContainsAny(searchableText,
                [
                    "family-friendly",
                    "family friendly",
                    "good with children",
                    "good with kids",
                    "around older children",
                    "family home",
                    "family routine"
                ]);
    }

    private static void AddPetEvidenceScores(
        AdoptionCopilotSearchDogsArgs args,
        string searchableText,
        List<string> reasons,
        ref int score)
    {
        var catsRequested = HasIntent(args, "cat", "cats");
        var dogsRequested = HasIntent(args, "other dogs", "good with dogs");
        if (catsRequested)
        {
            if (ContainsAny(searchableText,
                [
                    "passed the shelter cats calmly",
                    "passed calmly by the shelter cats",
                    "passed shelter cats calmly",
                    "calmly by the shelter cats"
                ]))
            {
                AddReason(reasons, "Calm near cats");
                score += 20;
                return;
            }

            if (ContainsAny(searchableText,
                [
                    "comfortable with cats",
                    "good with cats",
                    "cat-friendly",
                    "cat friendly"
                ]))
            {
                AddReason(reasons, "Calm near cats");
                score += 20;
                return;
            }

            if (ContainsAny(searchableText,
                [
                    "notices cats",
                    "redirected with treats",
                    "calm introductions",
                    "slow introductions to cats",
                    "slow introductions to smaller animals"
                ]))
            {
                AddReason(reasons, "Slow cat introductions");
                score += 12;
            }

            return;
        }

        if (dogsRequested)
        {
            if (ContainsAny(searchableText,
                [
                    "walks politely beside familiar calm dogs",
                    "comfortable with calm dogs",
                    "calm dog company",
                    "familiar calm dogs"
                ]))
            {
                AddReason(reasons, "Calm dog company");
                score += 16;
                return;
            }

            if (ContainsAny(searchableText,
                [
                    "good with dogs",
                    "comfortable with other dogs",
                    "social with other dogs",
                    "playful dogs after",
                    "enjoys sturdy, playful dogs",
                    "enjoys play sessions with steady dogs",
                    "dog-friendly",
                    "dog friendly"
                ]))
            {
                AddReason(reasons, "Enjoys dog company");
                score += 14;
                return;
            }

            if (ContainsAny(searchableText,
                [
                    "slow introductions",
                    "steady dogs over bouncy playmates",
                    "prefers steady dogs",
                    "more comfortable with calm dogs"
                ]))
            {
                AddReason(reasons, "Slow dog introductions");
                score += 10;
            }

            return;
        }

        if (HasIntent(args, "pets", "other pets") &&
            ContainsAny(searchableText,
                [
                    "good with cats",
                    "good with dogs",
                    "pet-friendly",
                    "pet friendly",
                    "comfortable with other dogs",
                    "comfortable with cats",
                    "passed the shelter cats calmly",
                    "walks politely beside familiar calm dogs"
                ]))
        {
            AddReason(reasons, "Pet compatibility notes");
            score += 12;
        }
    }

    private static bool IsSemanticReasonRelevantToCopilotArgs(string reason, AdoptionCopilotSearchDogsArgs args)
    {
        var lower = reason.ToLowerInvariant();
        if (lower.Contains("same city") || lower.Contains("near") || lower.Contains("distance") || lower.Contains("close"))
        {
            return !string.IsNullOrWhiteSpace(args.City) ||
                !string.IsNullOrWhiteSpace(args.Neighborhood) ||
                !string.IsNullOrWhiteSpace(args.NearLocationText);
        }

        if (lower.Contains("apartment") || lower.Contains("home") || lower.Contains("size"))
        {
            return IsApartmentRequest(args) || IsYardRequest(args) || args.Sizes?.Count > 0;
        }

        if (lower.Contains("family") || lower.Contains("children") || lower.Contains("kids"))
        {
            return args.GoodWithChildren == true || HasIntent(args, "children", "kids", "family");
        }

        if (lower.Contains("pet") || lower.Contains("cat") || lower.Contains("good with dogs") || lower.Contains("social"))
        {
            return args.GoodWithPets == true || HasIntent(args, "cats", "dogs", "pets", "other pets", "friendly", "social");
        }

        if (lower.Contains("experience") || lower.Contains("experienced"))
        {
            return HasIntent(args, "beginner", "first time", "experience", "experienced");
        }

        if (lower.Contains("activity") || lower.Contains("active") || lower.Contains("energetic"))
        {
            return string.Equals(args.EnergyLevel, "High", StringComparison.OrdinalIgnoreCase) ||
                HasIntent(args, "active", "energetic", "playful");
        }

        if (lower.Contains("calm") || lower.Contains("quiet") || lower.Contains("gentle") || lower.Contains("relaxed"))
        {
            return IsCalmRequest(args) || IsApartmentRequest(args);
        }

        if (lower.Contains("saved") || lower.Contains("favorite") || lower.Contains("recently viewed"))
        {
            return HasIntent(args, "saved", "favorites", "favorite", "similar");
        }

        return false;
    }

    private static bool HasSparseProfileText(string searchableText)
    {
        var wordCount = Regex.Matches(searchableText, @"\b[\p{L}\p{N}]+\b").Count;
        return wordCount < 24;
    }

    private static void AddReason(List<string> reasons, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) ||
            reasons.Any(existing => string.Equals(existing, reason.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        reasons.Add(reason.Trim());
    }

    private static bool HasIntent(AdoptionCopilotSearchDogsArgs args, params string[] terms)
    {
        var values = (args.BehaviorTerms ?? [])
            .Concat(args.TemperamentTags ?? [])
            .Concat(args.Temperaments ?? [])
            .Concat(args.Compatibility ?? [])
            .Append(args.Query ?? string.Empty)
            .Append(args.HousingPreference ?? string.Empty)
            .Append(args.HomeType ?? string.Empty)
            .Append(args.ActivityLevel ?? string.Empty)
            .Append(args.ExperienceLevel ?? string.Empty)
            .ToList();

        return terms.Any(term => values.Any(value => Contains(value, term)));
    }

    private static string NormalizePrimaryIntent(AdoptionCopilotSearchDogsArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.PrimaryIntent))
        {
            return args.PrimaryIntent.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        if (HasCatRequest(args) || HasChildrenRequest(args) || HasOtherDogsRequest(args))
        {
            return "Compatibility";
        }

        if (IsApartmentRequest(args))
        {
            return "HomeSuitability";
        }

        if (IsYardRequest(args) ||
            string.Equals(args.ActivityLevel, "High", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.EnergyLevel, "High", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.ActivityLevel, "Medium", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.EnergyLevel, "Medium", StringComparison.OrdinalIgnoreCase))
        {
            return "ActivityLevel";
        }

        if (args.Sizes?.Count > 0)
        {
            return "Size";
        }

        return "Temperament";
    }

    private static bool HasCompatibility(AdoptionCopilotSearchDogsArgs args, params string[] values)
    {
        return args.Compatibility?.Any(value =>
            values.Any(expected => string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase))) == true;
    }

    private static bool IsCompatibilityTarget(CopilotIntent intent, params string[] values)
    {
        return IsCompatibilityTarget(intent.CompatibilityTarget, values);
    }

    private static bool IsCompatibilityTarget(string? compatibilityTarget, params string[] values)
    {
        return values.Any(value => string.Equals(compatibilityTarget, value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasCatRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return HasCompatibility(args, "Cats") ||
            string.Equals(args.CompatibilityTarget, "Cats", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.CompatibilityTarget, "SmallAnimals", StringComparison.OrdinalIgnoreCase) ||
            (args.GoodWithPets == true && HasIntent(args, "cat", "cats")) ||
            HasIntent(args, "calm around cats", "slow introductions to cats");
    }

    private static bool HasChildrenRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return args.GoodWithChildren == true ||
            HasCompatibility(args, "Children", "OlderChildren") ||
            string.Equals(args.CompatibilityTarget, "Children", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.CompatibilityTarget, "OlderChildren", StringComparison.OrdinalIgnoreCase) ||
            HasIntent(args, "children", "kids", "family", "older children");
    }

    private static bool HasYoungChildrenRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return ContainsAny(args.Query,
            [
                "young children",
                "small children",
                "little children",
                "little kids",
                "toddlers",
                "toddler"
            ]);
    }

    private static bool HasExplicitLowActivityRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return HasIntent(args,
            "low activity",
            "less activity",
            "not too active",
            "not much exercise",
            "does not need much activity",
            "doesn t need much activity",
            "does not need too much activity",
            "doesn t need too much activity",
            "short walks",
            "slow walks");
    }

    private static bool HasExplicitLongerWalksRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return HasIntent(args,
            "longer walks",
            "long walks",
            "active walks",
            "brisk walks",
            "hiking");
    }

    private static bool HasExplicitDailyWalksRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return HasIntent(args,
            "daily walks",
            "regular walks");
    }

    private static bool HasExplicitModerateActivityRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return HasIntent(args,
            "moderate activity",
            "moderate exercise");
    }

    private static bool HasOtherDogsRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return HasCompatibility(args, "OtherDogs") ||
            string.Equals(args.CompatibilityTarget, "OtherDogs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.CompatibilityTarget, "SeniorDog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.CompatibilityTarget, "SensitiveDog", StringComparison.OrdinalIgnoreCase) ||
            args.Compatibility?.Any(value => Contains(value, "dog")) == true ||
            (args.GoodWithPets == true && HasIntent(args, "other dogs", "good with dogs", "another dog")) ||
            HasIntent(args, "calm dog company");
    }

    private static bool HasSeniorOrSensitiveHouseholdDogRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return string.Equals(args.CompatibilityTarget, "SeniorDog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.CompatibilityTarget, "SensitiveDog", StringComparison.OrdinalIgnoreCase) ||
            args.Compatibility?.Any(value =>
                string.Equals(value?.Trim(), "SeniorDog", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value?.Trim(), "SensitiveDog", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value?.Trim(), "SickDog", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value?.Trim(), "AnxiousDog", StringComparison.OrdinalIgnoreCase) ||
                Contains(value, "senior dog") ||
                Contains(value, "older dog") ||
                Contains(value, "old dog") ||
                Contains(value, "elderly dog") ||
                Contains(value, "sick dog") ||
                Contains(value, "recovering dog") ||
                Contains(value, "sensitive dog") ||
                Contains(value, "anxious dog")) == true;
    }

    private static bool HasYoungHouseholdDogRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return args.Compatibility?.Any(value =>
            string.Equals(value?.Trim(), "YoungDog", StringComparison.OrdinalIgnoreCase) ||
            Contains(value, "young dog") ||
            Contains(value, "playful dog") ||
            Contains(value, "puppy")) == true;
    }

    private static bool IsCalmRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return HasIntent(args, "calm", "quiet", "gentle", "relaxed", "low energy") ||
            string.Equals(args.EnergyLevel, "Low", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.ActivityLevel, "Low", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCalmSignal(string searchableText)
    {
        return ContainsAny(searchableText, CalmSignals);
    }

    private static bool HasCalmDogPreferenceSignal(string searchableText)
    {
        return ContainsAny(searchableText,
            [
                "more comfortable with calm dogs",
                "calm dogs are easier",
                "pushy dogs can make her retreat",
                "introductions should stay calm",
                "prefers steady dogs",
                "steady dogs over bouncy playmates",
                "comfortable with calm dogs"
            ]);
    }

    private static bool HasSeniorDogOverwhelmRisk(string searchableText)
    {
        if (ContainsAny(searchableText,
            [
                "more comfortable with calm dogs than very energetic",
                "calm dogs are easier",
                "pushy dogs can make her retreat",
                "introductions should stay calm",
                "prefers steady dogs over bouncy playmates",
                "steady dogs over bouncy playmates"
            ]))
        {
            return false;
        }

        return ContainsAny(searchableText,
            [
                "can overwhelm",
                "may overwhelm",
                "overwhelm shy dogs",
                "too intense",
                "pushy dogs",
                "rough play",
                "rough players",
                "very energetic playmates"
            ]);
    }

    private static bool HasActiveSignal(string searchableText)
    {
        return ContainsAny(searchableText, ActiveConflictSignals);
    }

    private static bool HasShortWalkEvidence(string searchableText)
    {
        return ContainsAny(searchableText, ["short daily walks", "short walks", "slow walks", "leash walks"]);
    }

    private static bool HasDailyWalkEvidence(string searchableText)
    {
        return ContainsAny(searchableText, ["daily walks", "regular walks"]) &&
            !ContainsAny(searchableText, ["short daily walks"]);
    }

    private static bool HasLongerWalkEvidence(string searchableText)
    {
        return ContainsAny(searchableText,
            [
                "longer walks",
                "long walks",
                "active walks",
                "brisk walks",
                "medium walk",
                "structured walks",
                "hiking"
            ]);
    }

    private static bool HasHigherActivityNeedSignal(string searchableText)
    {
        return ContainsAny(searchableText,
            [
                "longer walks",
                "brisk walks",
                "regular outdoor time",
                "regular outdoor play",
                "use his energy",
                "use her energy",
                "outdoor play",
                "space to run",
                "room to run",
                "open areas",
                "training games",
                "fetch",
                "medium walk",
                "exercise needs"
            ]);
    }

    private static bool HasExplicitApartmentFitEvidence(string searchableText)
    {
        return ContainsAny(searchableText,
            [
                "apartment",
                "flat",
                "smaller home",
                "smaller spaces",
                "small home",
                "quiet flat",
                "short daily walks",
                "indoor rest",
                "quiet evenings indoors",
                "does not need long outdoor sessions"
            ]);
    }

    private static bool IsApartmentRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return args.ApartmentFriendly == true ||
            ContainsAny(args.HousingPreference, ["apartment", "flat"]) ||
            ContainsAny(args.HomeType, ["apartment", "flat"]);
    }

    private static bool IsYardRequest(AdoptionCopilotSearchDogsArgs args)
    {
        return args.YardFriendly == true ||
            args.YardRequired == true ||
            args.NeedsYard == true ||
            ContainsAny(args.HousingPreference, ["yard", "garden", "house"]) ||
            ContainsAny(args.HomeType, ["yard", "garden", "house"]);
    }

    private static string BuildSearchableDogText(Dog dog)
    {
        return string.Join(' ', new[]
        {
            dog.Name,
            DogBreedFormatter.Format(dog),
            dog.Size.ToString(),
            dog.CoatColor,
            dog.Description,
            dog.BehaviorDescription,
            dog.MedicalStatus,
            dog.Location,
            dog.Shelter?.Name,
            dog.Shelter?.Neighborhood,
            dog.Shelter?.City
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static IReadOnlyList<string> BuildQueryTerms(AdoptionCopilotSearchDogsArgs args)
    {
        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(args.Query))
        {
            terms.AddRange(Tokenize(args.Query));
        }

        if (args.BehaviorTerms is not null)
        {
            terms.AddRange(args.BehaviorTerms.SelectMany(Tokenize));
        }

        if (args.TemperamentTags is not null)
        {
            terms.AddRange(args.TemperamentTags.SelectMany(Tokenize));
        }

        if (args.Temperaments is not null)
        {
            terms.AddRange(args.Temperaments.SelectMany(Tokenize));
        }

        if (args.Compatibility is not null)
        {
            terms.AddRange(args.Compatibility.SelectMany(Tokenize));
        }

        if (args.CoatColors is not null)
        {
            terms.AddRange(args.CoatColors.SelectMany(Tokenize));
        }

        if (args.MustHave is not null)
        {
            terms.AddRange(args.MustHave.SelectMany(Tokenize));
        }

        if (args.NiceToHave is not null)
        {
            terms.AddRange(args.NiceToHave.SelectMany(Tokenize));
        }

        terms.AddRange(Tokenize(args.EnergyLevel));
        terms.AddRange(Tokenize(args.ActivityLevel));
        terms.AddRange(Tokenize(args.HomeType));
        terms.AddRange(Tokenize(args.HousingPreference));

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> MergeValues(params IEnumerable<string>?[] valueSets)
    {
        return valueSets
            .Where(values => values is not null)
            .SelectMany(values => values!)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string>? SplitSingle(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : [value.Trim()];
    }

    private static IReadOnlyList<string> NormalizeTemperamentValues(IEnumerable<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => !ContainsAny(value,
                [
                    "apartment",
                    "flat",
                    "yard",
                    "garden",
                    "house",
                    "active",
                    "cat",
                    "dog",
                    "children",
                    "kids",
                    "walk",
                    "walks",
                    "exercise",
                    "introduction",
                    "introductions",
                    "settle",
                    "settles",
                    "short walk",
                    "long walk",
                    "longer walk",
                    "daily walk",
                    "regular walk",
                    "brisk walk",
                    "indoor",
                    "routine",
                    "low activity",
                    "moderate activity",
                    "high activity",
                    "activity"
                ]))
            .Select(value => value switch
            {
                var text when text.Equals("calm", StringComparison.OrdinalIgnoreCase) => "Calm / gentle",
                var text when text.Equals("friendly", StringComparison.OrdinalIgnoreCase) => "Friendly",
                var text when text.Equals("gentle", StringComparison.OrdinalIgnoreCase) => "Calm / gentle",
                var text when text.Equals("playful", StringComparison.OrdinalIgnoreCase) => "Playful",
                var text when text.Equals("shy", StringComparison.OrdinalIgnoreCase) => "Shy",
                var text when text.Equals("anxious", StringComparison.OrdinalIgnoreCase) => "Anxious",
                _ => value
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static IReadOnlyList<string> NormalizeCompatibilityValues(IEnumerable<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Select(value => value switch
            {
                var text when text.Equals("children", StringComparison.OrdinalIgnoreCase) => "Children",
                var text when text.Equals("kids", StringComparison.OrdinalIgnoreCase) => "Children",
                var text when text.Equals("olderchildren", StringComparison.OrdinalIgnoreCase) => "Older children",
                var text when text.Equals("older children", StringComparison.OrdinalIgnoreCase) => "Older children",
                var text when text.Equals("cats", StringComparison.OrdinalIgnoreCase) => "Cats",
                var text when text.Equals("cat", StringComparison.OrdinalIgnoreCase) => "Cats",
                var text when text.Equals("dogs", StringComparison.OrdinalIgnoreCase) => "Other dogs",
                var text when text.Equals("otherdogs", StringComparison.OrdinalIgnoreCase) => "Other dogs",
                var text when text.Equals("other dogs", StringComparison.OrdinalIgnoreCase) => "Other dogs",
                var text when text.Equals("seniordog", StringComparison.OrdinalIgnoreCase) => "Senior dog",
                var text when text.Equals("senior dog", StringComparison.OrdinalIgnoreCase) => "Senior dog",
                var text when text.Equals("olderdog", StringComparison.OrdinalIgnoreCase) => "Senior dog",
                var text when text.Equals("youngdog", StringComparison.OrdinalIgnoreCase) => "Young dog",
                var text when text.Equals("young dog", StringComparison.OrdinalIgnoreCase) => "Young dog",
                var text when text.Equals("sickdog", StringComparison.OrdinalIgnoreCase) => "Sensitive dog",
                var text when text.Equals("sick dog", StringComparison.OrdinalIgnoreCase) => "Sensitive dog",
                var text when text.Equals("anxiousdog", StringComparison.OrdinalIgnoreCase) => "Sensitive dog",
                var text when text.Equals("anxious dog", StringComparison.OrdinalIgnoreCase) => "Sensitive dog",
                _ => value
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static string? FormatLifestyleConstraint(string? energyLevel)
    {
        return energyLevel?.Trim() switch
        {
            "Low" => "Low activity",
            "High" => "Active",
            "Medium" => "Moderate activity",
            _ => null
        };
    }

    private static string? FormatLifestyleConstraint(AdoptionCopilotSearchDogsArgs args)
    {
        var energyLevel = args.EnergyLevel ?? args.ActivityLevel;
        if (ContainsAny(energyLevel, ["low"]) && HasSeniorOrSensitiveHouseholdDogRequest(args))
        {
            return "Calm";
        }

        return FormatLifestyleConstraint(energyLevel);
    }

    private static IReadOnlyList<string> FormatHomeConstraints(AdoptionCopilotSearchDogsArgs args)
    {
        var values = new List<string>();
        if (IsApartmentRequest(args))
        {
            values.Add("Apartment");
        }

        if (IsYardRequest(args))
        {
            values.Add("House with yard");
        }

        if (values.Count == 0 && !string.IsNullOrWhiteSpace(args.HomeType))
        {
            values.Add(args.HomeType.Trim());
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<AdoptionCopilotConstraint> BuildAppliedConstraints(AdoptionCopilotSearchDogsArgs args, CopilotIntent intent)
    {
        var constraints = new List<AdoptionCopilotConstraint>();
        AddConstraint(constraints, "Size", args.Sizes);
        AddConstraint(constraints, "Breed", args.Breeds);
        AddConstraint(constraints, "Coat color", args.CoatColors);
        AddConstraint(constraints, "Status", intent.Statuses);
        AddConstraint(constraints, "Location", MergeValues(SplitSingle(args.Neighborhood), SplitSingle(args.City)));
        AddConstraint(constraints, "Shelter", args.ShelterName);
        var ageConstraint = FormatAgeConstraint(args);
        if (!string.IsNullOrWhiteSpace(ageConstraint))
        {
            constraints.Add(new AdoptionCopilotConstraint("Age", ageConstraint));
        }

        if (!string.IsNullOrWhiteSpace(args.NearLocationText))
        {
            constraints.Add(new AdoptionCopilotConstraint("Near", args.RadiusKm.HasValue
                ? $"{args.NearLocationText.Trim()}, {args.RadiusKm.Value} km"
                : args.NearLocationText.Trim()));
        }

        AddConstraint(constraints, "Temperament", NormalizeTemperamentValues(MergeValues(args.Temperaments, args.TemperamentTags, args.BehaviorTerms)));

        if (intent.PrimaryIntent == "Compatibility" && intent.CompatibilityTarget != "None")
        {
            AddConstraint(constraints, "Compatibility", FormatCompatibilityTarget(intent.CompatibilityTarget));
        }
        else
        {
            AddConstraint(constraints, "Compatibility", NormalizeCompatibilityValues(args.Compatibility));
        }

        AddConstraint(constraints, "Lifestyle", intent.ActivityLevel == "Low" && IsCompatibilityTarget(intent, "SeniorDog", "SensitiveDog")
            ? "Calm"
            : FormatLifestyleConstraint(args));
        AddConstraint(constraints, "Activity", FormatActivityConstraints(args));

        AddConstraint(constraints, "Home", FormatHomeConstraints(args));

        return AdoptionCopilotConstraintNormalizer.Normalize(constraints);
    }

    private static IReadOnlyList<string> FormatActivityConstraints(AdoptionCopilotSearchDogsArgs args)
    {
        var values = new List<string>();
        if (HasIntent(args, "short walks", "short daily walks", "slow walks", "leash walks"))
        {
            values.Add("Short walks");
        }
        else if (HasExplicitLongerWalksRequest(args))
        {
            values.Add("Longer walks");
        }
        else if (HasExplicitDailyWalksRequest(args))
        {
            values.Add("Daily walks");
        }

        if (HasExplicitModerateActivityRequest(args))
        {
            values.Add("Moderate activity");
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddConstraint(List<AdoptionCopilotConstraint> constraints, string label, IEnumerable<string>? values)
    {
        var cleanValues = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanValues?.Count > 0)
        {
            constraints.Add(new AdoptionCopilotConstraint(label, string.Join(", ", cleanValues)));
        }
    }

    private static void AddConstraint(List<AdoptionCopilotConstraint> constraints, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            constraints.Add(new AdoptionCopilotConstraint(label, value.Trim()));
        }
    }

    private static CopilotIntent AnalyzeIntent(AdoptionCopilotSearchDogsArgs args)
    {
        var primaryIntent = NormalizePrimaryIntent(args);
        var compatibilityTarget = NormalizeCompatibilityTarget(args) ?? "None";
        var homeType = NormalizeHomeType(args.HomeType ?? args.HousingPreference) ?? "Any";
        var activityLevel = EmptyToNull(args.ActivityLevel) ?? EmptyToNull(args.EnergyLevel) ?? "Any";
        if (activityLevel == "Any" && IsCompatibilityTarget(compatibilityTarget, "SeniorDog", "SensitiveDog"))
        {
            activityLevel = "Low";
        }

        var mustHave = MergeValues(args.MustHave, args.DesiredTraits).ToList();
        var niceToHave = MergeValues(args.NiceToHave, args.EvidenceToLookFor).ToList();
        var negative = MergeValues(args.Avoid, args.AvoidTraits).ToList();
        var secondary = new List<string>();

        AddIntentDefaults(primaryIntent, compatibilityTarget, homeType, activityLevel, mustHave, niceToHave, negative, secondary);
        if (HasYoungChildrenRequest(args))
        {
            secondary.Add("young children");
        }

        var statuses = (args.Statuses?.Count > 0 ? args.Statuses : [DogStatus.Available.ToString(), DogStatus.Reserved.ToString()])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var sizes = args.Sizes?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        return new CopilotIntent(
            primaryIntent,
            compatibilityTarget,
            homeType,
            activityLevel,
            BuildRealLifeNeed(primaryIntent, compatibilityTarget, homeType, activityLevel),
            mustHave.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            niceToHave.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            negative.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            secondary.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            BuildIntentChips(primaryIntent, compatibilityTarget, homeType, activityLevel, statuses, sizes),
            statuses,
            EmptyToNull(args.City),
            EmptyToNull(args.Neighborhood),
            sizes,
            Math.Clamp(args.Limit ?? args.Count, 1, 20));
    }

    private static void AddIntentDefaults(
        string primaryIntent,
        string compatibilityTarget,
        string homeType,
        string activityLevel,
        List<string> mustHave,
        List<string> niceToHave,
        List<string> negative,
        List<string> secondary)
    {
        if (primaryIntent == "Compatibility" && IsCompatibilityTarget(compatibilityTarget, "Cats", "SmallAnimals"))
        {
            mustHave.AddRange(["calm near cats", "redirectable around cats", "slow cat introductions", "low chase interest"]);
            negative.AddRange(["chase behavior", "strong interest in fast-moving small animals", "not suitable with cats"]);
            return;
        }

        if (primaryIntent == "Compatibility" && IsCompatibilityTarget(compatibilityTarget, "SeniorDog", "SensitiveDog"))
        {
            mustHave.AddRange(["calm dog company", "respectful around dogs", "gentle play style", "slow introductions", "not pushy"]);
            negative.AddRange(["rough play", "very energetic", "pushy with other dogs", "overwhelms calm dogs", "needs to be only dog"]);
            secondary.AddRange(["quiet routine", "settles easily", "low or medium activity"]);
            return;
        }

        if (primaryIntent == "Compatibility" && IsCompatibilityTarget(compatibilityTarget, "Children", "OlderChildren"))
        {
            mustHave.AddRange(["gentle handling", "predictable family routine", "relaxed around children"]);
            negative.AddRange(["easily overwhelmed by noise", "not suitable for young children", "mouthy rough play"]);
            return;
        }

        if (homeType == "Apartment" && activityLevel is ("Medium" or "High"))
        {
            mustHave.AddRange(["apartment fit", "longer walks", "daily walks", "moderate activity"]);
            negative.AddRange(["needs lots of outdoor space", "requires a yard"]);
            return;
        }

        if (activityLevel == "Medium")
        {
            mustHave.AddRange(["longer walks", "daily walks", "moderate activity"]);
            negative.AddRange(["very low activity", "very high activity"]);
            return;
        }

        if (homeType == "Apartment" || activityLevel == "Low")
        {
            mustHave.AddRange(["short walks", "indoor rest", "settles quickly", "quiet routine", "small or medium size"]);
            negative.AddRange(["very energetic", "needs lots of outdoor space", "rough high activity needs"]);
        }

        if (homeType == "House with yard" || activityLevel == "High")
        {
            mustHave.AddRange(["outdoor play", "longer walks", "training games", "space to run"]);
            negative.AddRange(["very low activity", "timid outdoors"]);
        }
    }

    private static string BuildRealLifeNeed(string primaryIntent, string compatibilityTarget, string homeType, string activityLevel)
    {
        if (primaryIntent == "Compatibility" && compatibilityTarget == "SensitiveDog")
        {
            return "The user needs a dog that will not overwhelm a sick, anxious, or recovering dog already at home.";
        }

        if (primaryIntent == "Compatibility" && compatibilityTarget == "SeniorDog")
        {
            return "The user needs a dog that will respect an older dog and not pressure them into rough play.";
        }

        if (primaryIntent == "Compatibility" && IsCompatibilityTarget(compatibilityTarget, "Cats", "SmallAnimals"))
        {
            return "The user needs a dog that may safely live with or be introduced to cats or small animals.";
        }

        if (primaryIntent == "Compatibility" && IsCompatibilityTarget(compatibilityTarget, "Children", "OlderChildren"))
        {
            return "The user needs a dog with evidence for safe, predictable family interactions.";
        }

        if (homeType == "Apartment" && activityLevel is ("Medium" or "High"))
        {
            return "The user needs an apartment-suitable dog that can also enjoy regular or longer walks.";
        }

        if (homeType == "Apartment" || activityLevel == "Low")
        {
            return "The user needs a dog suitable for a smaller home and lower daily activity.";
        }

        if (homeType == "House with yard" || activityLevel == "High")
        {
            return "The user wants a dog suited to regular outdoor activity and a more active home.";
        }

        return "The user needs a public-safe dog whose profile evidence matches the request.";
    }

    private static List<string> BuildIntentChips(
        string primaryIntent,
        string compatibilityTarget,
        string homeType,
        string activityLevel,
        IReadOnlyList<string> statuses,
        IReadOnlyList<string> sizes)
    {
        var chips = new List<string>();
        if (primaryIntent == "Compatibility" && compatibilityTarget != "None")
        {
            chips.Add($"Compatibility: {FormatCompatibilityTarget(compatibilityTarget)}");
        }

        if (homeType == "Apartment")
        {
            chips.Add("Home: Apartment");
        }
        else if (homeType == "House with yard")
        {
            chips.Add("Home: House with yard");
        }

        if (activityLevel == "Low" && IsCompatibilityTarget(compatibilityTarget, "SeniorDog", "SensitiveDog"))
        {
            chips.Add("Lifestyle: Calm");
        }
        else if (activityLevel == "Low")
        {
            chips.Add("Lifestyle: Low activity");
        }
        else if (activityLevel == "High")
        {
            chips.Add("Lifestyle: Active");
        }
        else if (activityLevel == "Medium")
        {
            chips.Add("Lifestyle: Moderate activity");
        }

        if (sizes.Count > 0)
        {
            chips.Add($"Size: {string.Join(", ", sizes)}");
        }

        if (statuses.Count > 0)
        {
            chips.Add($"Status: {string.Join(", ", statuses)}");
        }

        return chips;
    }

    private static string FormatCompatibilityTarget(string compatibilityTarget)
    {
        return compatibilityTarget switch
        {
            "Cats" => "Cats",
            "SmallAnimals" => "Small animals",
            "Children" => "Children",
            "OlderChildren" => "Older children",
            "SeniorDog" => "Senior dog",
            "SensitiveDog" => "Sensitive dog",
            "YoungDog" => "Young dog",
            "OtherDogs" => "Other dogs",
            _ => compatibilityTarget
        };
    }

    private static void NormalizeOptionalArguments(AdoptionCopilotSearchDogsArgs args)
    {
        args.PrimaryIntent = EmptyToNull(args.PrimaryIntent);
        args.CompatibilityTarget = EmptyToNull(args.CompatibilityTarget);
        args.EnergyLevel = EmptyToNull(args.EnergyLevel) ?? EmptyToNull(args.ActivityLevel);
        args.ActivityLevel = EmptyToNull(args.ActivityLevel) ?? EmptyToNull(args.EnergyLevel);
        args.HomeType = NormalizeHomeType(EmptyToNull(args.HomeType));
        args.HousingPreference = EmptyToNull(args.HousingPreference) ?? args.HomeType;
        args.ExperienceLevel = EmptyToNull(args.ExperienceLevel);
        args.TemperamentTags = MergeValues(args.TemperamentTags, args.Temperaments).ToList();
        args.BehaviorTerms = MergeValues(args.BehaviorTerms, args.Temperaments).ToList();
        args.CoatColors = args.CoatColors?
            .Select(DogCoatColorOptions.Normalize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        args.MustHave = MergeValues(args.MustHave, args.DesiredTraits).ToList();
        args.NiceToHave = MergeValues(args.NiceToHave, args.EvidenceToLookFor).ToList();
        args.Avoid = MergeValues(args.Avoid, args.AvoidTraits).ToList();

        if (args.Compatibility?.Any(value => Contains(value, "children") || Contains(value, "kids")) == true)
        {
            args.GoodWithChildren ??= true;
        }

        if (args.Compatibility?.Any(value => Contains(value, "cat") || Contains(value, "dog")) == true)
        {
            args.GoodWithPets ??= true;
        }

        args.CompatibilityTarget ??= NormalizeCompatibilityTarget(args);
        args.PrimaryIntent ??= InferPrimaryIntent(args);

        if (string.Equals(args.HomeType, "Apartment", StringComparison.OrdinalIgnoreCase))
        {
            args.ApartmentFriendly ??= true;
        }

        if (string.Equals(args.HomeType, "House with yard", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args.HomeType, "HouseWithYard", StringComparison.OrdinalIgnoreCase))
        {
            args.YardFriendly ??= true;
            args.NeedsYard ??= true;
        }

        if (args.MaxAgeYears is <= 0)
        {
            args.MaxAgeYears = null;
        }

        if (args.MinAgeYears is <= 0)
        {
            args.MinAgeYears = null;
        }

        if (args.MaxAgeYears is null && args.MinAgeYears is null)
        {
            args.AgeComparison = null;
        }
        else if (args.MaxAgeYears is not null &&
            !string.Equals(args.AgeComparison, "Under", StringComparison.OrdinalIgnoreCase))
        {
            args.AgeComparison = "Max";
        }
        else if (args.MinAgeYears is not null)
        {
            args.AgeComparison = "AtLeast";
        }

        if (args.RadiusKm is <= 0)
        {
            args.RadiusKm = null;
        }

        if (args.Limit is <= 0)
        {
            args.Limit = null;
        }
    }

    private static string? NormalizeCompatibilityTarget(AdoptionCopilotSearchDogsArgs args)
    {
        if (args.Compatibility?.Any(value =>
            Contains(value, "olderchildren") ||
            Contains(value, "older children") ||
            string.Equals(value?.Trim(), "OlderChildren", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "OlderChildren";
        }

        if (args.Compatibility?.Any(value =>
            Contains(value, "children") ||
            Contains(value, "kids") ||
            string.Equals(value?.Trim(), "Children", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "Children";
        }

        if (args.Compatibility?.Any(value =>
            Contains(value, "cat") ||
            string.Equals(value?.Trim(), "Cats", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "Cats";
        }

        if (args.Compatibility?.Any(value =>
            Contains(value, "small animal") ||
            string.Equals(value?.Trim(), "SmallAnimals", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "SmallAnimals";
        }

        if (args.Compatibility?.Any(value =>
            Contains(value, "senior dog") ||
            Contains(value, "older dog") ||
            Contains(value, "old dog") ||
            Contains(value, "elderly dog") ||
            string.Equals(value?.Trim(), "SeniorDog", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "SeniorDog";
        }

        if (args.Compatibility?.Any(value =>
            Contains(value, "sick dog") ||
            Contains(value, "recovering dog") ||
            Contains(value, "ill dog") ||
            Contains(value, "sensitive dog") ||
            Contains(value, "anxious dog") ||
            string.Equals(value?.Trim(), "SickDog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value?.Trim(), "SensitiveDog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value?.Trim(), "AnxiousDog", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "SensitiveDog";
        }

        if (args.Compatibility?.Any(value =>
            Contains(value, "young dog") ||
            Contains(value, "playful dog") ||
            Contains(value, "puppy") ||
            string.Equals(value?.Trim(), "YoungDog", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return "YoungDog";
        }

        if (args.Compatibility?.Any(value => Contains(value, "dog")) == true)
        {
            return "OtherDogs";
        }

        return null;
    }

    private static string InferPrimaryIntent(AdoptionCopilotSearchDogsArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.CompatibilityTarget) ||
            args.Compatibility?.Count > 0 ||
            args.GoodWithChildren == true ||
            args.GoodWithPets == true)
        {
            return "Compatibility";
        }

        if (IsApartmentRequest(args))
        {
            return "HomeSuitability";
        }

        if (IsYardRequest(args) ||
            !string.IsNullOrWhiteSpace(args.ActivityLevel) ||
            !string.IsNullOrWhiteSpace(args.EnergyLevel))
        {
            return "ActivityLevel";
        }

        if (args.Sizes?.Count > 0)
        {
            return "Size";
        }

        if (!string.IsNullOrWhiteSpace(args.City) ||
            !string.IsNullOrWhiteSpace(args.Neighborhood) ||
            !string.IsNullOrWhiteSpace(args.NearLocationText))
        {
            return "Location";
        }

        if (!string.IsNullOrWhiteSpace(args.ExperienceLevel))
        {
            return "ExperienceLevel";
        }

        return "Temperament";
    }

    private static string? NormalizeHomeType(string? homeType)
    {
        if (string.IsNullOrWhiteSpace(homeType))
        {
            return null;
        }

        if (ContainsAny(homeType, ["apartment", "flat"]))
        {
            return "Apartment";
        }

        if (ContainsAny(homeType, ["house with yard", "housewithyard", "yard", "garden"]))
        {
            return "House with yard";
        }

        return homeType.Trim();
    }

    private static bool MatchesAgeConstraint(Dog dog, AdoptionCopilotSearchDogsArgs args)
    {
        if (args.MaxAgeYears is > 0)
        {
            if (string.Equals(args.AgeComparison, "Under", StringComparison.OrdinalIgnoreCase))
            {
                return dog.AgeYears < args.MaxAgeYears.Value;
            }

            if (dog.AgeYears > args.MaxAgeYears.Value ||
                (dog.AgeYears == args.MaxAgeYears.Value && dog.AgeMonths > 0))
            {
                return false;
            }
        }

        if (args.MinAgeYears is > 0 && dog.AgeYears < args.MinAgeYears.Value)
        {
            return false;
        }

        return true;
    }

    private static string? FormatAgeConstraint(AdoptionCopilotSearchDogsArgs args)
    {
        if (args.MaxAgeYears is > 0)
        {
            return string.Equals(args.AgeComparison, "Under", StringComparison.OrdinalIgnoreCase)
                ? $"under {args.MaxAgeYears.Value} {PluralizeYear(args.MaxAgeYears.Value)}"
                : $"max {args.MaxAgeYears.Value} {PluralizeYear(args.MaxAgeYears.Value)}";
        }

        return args.MinAgeYears is > 0
            ? $"at least {args.MinAgeYears.Value} {PluralizeYear(args.MinAgeYears.Value)}"
            : null;
    }

    private static string PluralizeYear(int years)
    {
        return years == 1 ? "year" : "years";
    }

    private static HashSet<DogSize> ParseSizes(IEnumerable<string>? values)
    {
        return values?
            .Select(ParseSize)
            .Where(size => size.HasValue)
            .Select(size => size!.Value)
            .ToHashSet() ?? [];
    }

    private static DogSize? ParseSize(string? value)
    {
        return Enum.TryParse<DogSize>(value?.Trim(), true, out var size) ? size : null;
    }

    private static HashSet<DogStatus> ParseStatuses(IEnumerable<string>? values)
    {
        return values?
            .Select(ParseStatus)
            .Where(status => status is DogStatus.Available or DogStatus.Reserved)
            .Select(status => status!.Value)
            .ToHashSet() ?? [];
    }

    private static HashSet<string> ParseCoatColors(IEnumerable<string>? values)
    {
        return values?
            .Select(DogCoatColorOptions.Normalize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    }

    private static DogStatus? ParseStatus(string? value)
    {
        return Enum.TryParse<DogStatus>(value?.Trim().Replace(" ", ""), true, out var status) ? status : null;
    }

    private static List<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 2 && term is not "dog" and not "dogs" and not "for" and not "near" and not "with")
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<string> MostCommon(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(3)
            .Select(group => group.Key)
            .ToList();
    }

    private static bool Contains(string? value, string? term)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !string.IsNullOrWhiteSpace(term) &&
            value.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string? value, IEnumerable<string> terms)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNearestSort(string? sort)
    {
        return string.Equals(sort, "nearest", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sort, "nearest_first", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMatchLabel(int scorePercent)
    {
        return scorePercent switch
        {
            >= 80 => "Strong match",
            >= 65 => "Good match",
            >= 45 => "Possible match",
            _ => "Low match"
        };
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
