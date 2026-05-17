using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class AdoptionCopilotService(
    ApplicationDbContext context,
    IAdoptionCopilotToolService toolService,
    IOpenAiAdoptionCopilotClient openAiCopilotClient,
    IOptions<OpenAiSettings> openAiOptions,
    ILogger<AdoptionCopilotService> logger) : IAdoptionCopilotService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdoptionCopilotResponse> AskAsync(
        string adopterUserId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var query = userMessage.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new AdoptionCopilotResponse(
                "Tell me what kind of dog you are looking for, and I will suggest real PawConnect matches.",
                [],
                false,
                false,
                false);
        }

        var deterministicArgs = await BuildDeterministicSearchArgsAsync(query, cancellationToken);
        if (HasUnresolvedNeighborhoodIntent(query, deterministicArgs.Neighborhood))
        {
            return new AdoptionCopilotResponse(
                "Please name a specific neighborhood, such as Zorilor, Mănăștur, Bună Ziua, or Gheorgheni. PawConnect will not broaden a neighborhood request without a clear area.",
                [],
                false,
                false,
                false,
                null,
                BuildDeterministicConstraintPreview(deterministicArgs));
        }

        var fallbackSearch = await toolService.SearchDogsAsync(adopterUserId, deterministicArgs, cancellationToken);
        var fallback = BuildFallbackResponse(query, fallbackSearch, "AI assistance is unavailable right now, so PawConnect used safe rule-based search.");
        var settings = openAiOptions.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            return fallback;
        }

        var candidateMap = fallbackSearch.Dogs.ToDictionary(candidate => candidate.DogId);
        Dictionary<int, AdoptionCopilotToolDogCandidate>? latestSearchCandidateMap = null;
        AdoptionCopilotToolSearchResult? latestSearchResult = null;
        var usedSemanticSearch = fallbackSearch.UsedSemanticSearch;
        var appliedConstraints = fallbackSearch.AppliedConstraints.ToList();
        var toolNamesCalled = new List<string>();

        try
        {
            var openAiResponse = await openAiCopilotClient.AskWithToolsAsync(
                new AdoptionCopilotToolOpenAiRequest(query, appliedConstraints),
                async (toolCall, token) =>
                {
                    toolNamesCalled.Add(toolCall.Name);
                    var output = await ExecuteToolCallAsync(adopterUserId, deterministicArgs, toolCall, token);
                    if (output.SearchResult is not null)
                    {
                        latestSearchResult = output.SearchResult;
                        latestSearchCandidateMap = output.SearchResult.Dogs.ToDictionary(candidate => candidate.DogId);
                        usedSemanticSearch |= output.SearchResult.UsedSemanticSearch;
                        foreach (var constraint in output.SearchResult.AppliedConstraints)
                        {
                            if (!appliedConstraints.Any(existing =>
                                    string.Equals(existing.Label, constraint.Label, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(existing.Value, constraint.Value, StringComparison.OrdinalIgnoreCase)))
                            {
                                appliedConstraints.Add(constraint);
                            }
                        }

                        foreach (var candidate in output.SearchResult.Dogs)
                        {
                            candidateMap[candidate.DogId] = candidate;
                        }
                    }

                    if (output.DogCandidate is not null)
                    {
                        candidateMap[output.DogCandidate.DogId] = output.DogCandidate;
                    }

                    return new OpenAiCopilotToolOutput(toolCall.CallId, toolCall.Name, output.OutputJson);
                },
                cancellationToken);

            logger.LogInformation(
                "Adoption Copilot used OpenAI model {Model} with tools {ToolNames} and {CandidateCount} candidate dogs.",
                settings.GetSafeChatModel(),
                string.Join(", ", toolNamesCalled.Distinct(StringComparer.OrdinalIgnoreCase)),
                candidateMap.Count);

            if (!openAiResponse.Success || openAiResponse.Results.Count == 0)
            {
                return latestSearchResult is not null
                    ? BuildFallbackFromCandidates(
                        query,
                        latestSearchResult.Dogs,
                        usedSemanticSearch,
                        true,
                        appliedConstraints,
                        openAiResponse.ErrorMessage ?? fallback.FallbackReason,
                        latestSearchResult.EmptyReason)
                    : fallback with
                    {
                        FallbackReason = openAiResponse.ErrorMessage ?? fallback.FallbackReason,
                        AppliedConstraints = appliedConstraints
                    };
            }

            var allowedCandidateMap = latestSearchCandidateMap ?? candidateMap;
            var aiResults = openAiResponse.Results
                .Where(result => allowedCandidateMap.ContainsKey(result.DogId))
                .OrderBy(result => result.Rank)
                .Select(result => BuildAiResult(result, allowedCandidateMap[result.DogId], appliedConstraints))
                .ToList();

            if (aiResults.Count == 0)
            {
                return BuildFallbackFromCandidates(
                    query,
                    allowedCandidateMap.Values.ToList(),
                    usedSemanticSearch,
                    true,
                    appliedConstraints,
                    "OpenAI returned no valid PawConnect dog IDs.",
                    latestSearchResult?.EmptyReason);
            }

            var aiIds = aiResults.Select(result => result.DogId).ToHashSet();
            aiResults.AddRange(allowedCandidateMap.Values
                .Where(candidate => !aiIds.Contains(candidate.DogId))
                .OrderByDescending(candidate => candidate.ScorePercent)
                .Take(Math.Max(0, 6 - aiResults.Count))
                .Select(candidate => BuildFallbackDogResult(candidate, appliedConstraints)));

            return new AdoptionCopilotResponse(
                NormalizeAssistantMessage(openAiResponse.AssistantMessage, query, aiResults.FirstOrDefault()?.Dog.Name, appliedConstraints),
                aiResults.Take(6).ToList(),
                true,
                usedSemanticSearch,
                true,
                null,
                appliedConstraints);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Adoption Copilot tool-calling flow failed. Returning safe fallback results.");
            return fallback;
        }
    }

    private async Task<CopilotToolExecutionResult> ExecuteToolCallAsync(
        string adopterUserId,
        AdoptionCopilotSearchDogsArgs deterministicArgs,
        OpenAiCopilotToolCall toolCall,
        CancellationToken cancellationToken)
    {
        switch (toolCall.Name)
        {
            case "search_dogs":
            {
                var args = DeserializeArgs<AdoptionCopilotSearchDogsArgs>(toolCall.ArgumentsJson) ?? new AdoptionCopilotSearchDogsArgs();
                MergeDeterministicConstraints(args, deterministicArgs);
                var result = await toolService.SearchDogsAsync(adopterUserId, args, cancellationToken);
                var json = JsonSerializer.Serialize(new AdoptionCopilotToolJsonResult(
                    result.Dogs.Count > 0,
                    result.EmptyReason,
                    result.Dogs.Select(ToDogDto).ToList(),
                    result.AppliedConstraints), JsonOptions);
                return new CopilotToolExecutionResult(json, result, null);
            }
            case "get_adopter_profile_summary":
            {
                var profile = await toolService.GetAdopterProfileSummaryAsync(adopterUserId, cancellationToken);
                return new CopilotToolExecutionResult(JsonSerializer.Serialize(profile, JsonOptions), null, null);
            }
            case "get_favorite_and_recent_preferences":
            {
                var preferences = await toolService.GetFavoriteAndRecentPreferencesAsync(adopterUserId, cancellationToken);
                return new CopilotToolExecutionResult(JsonSerializer.Serialize(preferences, JsonOptions), null, null);
            }
            case "get_dog_details_public":
            {
                var args = DeserializeArgs<DogDetailsToolArgs>(toolCall.ArgumentsJson);
                var candidate = args is null ? null : await toolService.GetDogDetailsPublicAsync(args.DogId, cancellationToken);
                object payload = candidate is null
                    ? new { success = false, message = "Dog is not public-safe or was not found." }
                    : new { success = true, dog = ToDogDto(candidate) };
                var json = JsonSerializer.Serialize(payload, JsonOptions);
                return new CopilotToolExecutionResult(json, null, candidate);
            }
            default:
                return new CopilotToolExecutionResult(
                    JsonSerializer.Serialize(new { success = false, message = "Unknown tool." }, JsonOptions),
                    null,
                    null);
        }
    }

    private static T? DeserializeArgs<T>(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(argumentsJson, JsonOptions);
    }

    private async Task<AdoptionCopilotSearchDogsArgs> BuildDeterministicSearchArgsAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var sizes = DetectSizes(query);
        var statuses = DetectStatuses(query);
        var ageConstraint = DetectAgeConstraint(query);
        var neighborhood = await DetectExplicitNeighborhoodAsync(query, cancellationToken);
        var behaviorTerms = DetectBehaviorTerms(query);
        var temperamentTags = DetectTemperamentTags(query);
        var homeType = DetectHomeType(query);
        var activityLevel = DetectEnergyLevel(query) ?? DetectHouseholdDogActivityLevel(query);
        var compatibility = DetectCompatibility(query);
        var primaryIntent = DetectPrimaryIntent(query, compatibility, homeType, activityLevel, sizes);
        var compatibilityTarget = DetectCompatibilityTarget(compatibility);
        var city = DetectCity(query);
        var apartmentFriendly = DetectApartmentFriendly(query);
        var yardFriendly = DetectYardFriendly(query);
        var mustHave = DetectMustHaveSignals(query);
        var niceToHave = DetectNiceToHaveSignals(query);
        var avoid = DetectAvoidSignals(query);
        return new AdoptionCopilotSearchDogsArgs
        {
            Query = query,
            PrimaryIntent = primaryIntent,
            Sizes = sizes.Count > 0 ? sizes : null,
            Statuses = statuses.Count > 0 ? statuses : [DogStatus.Available.ToString(), DogStatus.Reserved.ToString()],
            City = city,
            MaxAgeYears = ageConstraint.MaxAgeYears,
            MinAgeYears = ageConstraint.MinAgeYears,
            AgeComparison = ageConstraint.Comparison,
            Neighborhood = neighborhood,
            BehaviorTerms = behaviorTerms.Count > 0 ? behaviorTerms : null,
            TemperamentTags = temperamentTags.Count > 0 ? temperamentTags : null,
            Temperaments = temperamentTags.Count > 0 ? temperamentTags : null,
            Compatibility = compatibility.Count > 0 ? compatibility : null,
            CompatibilityTarget = compatibilityTarget,
            EnergyLevel = activityLevel,
            ActivityLevel = activityLevel,
            HomeType = homeType,
            HousingPreference = homeType ?? DetectHousingPreference(query),
            ApartmentFriendly = apartmentFriendly,
            YardFriendly = yardFriendly,
            YardRequired = DetectYardRequired(query),
            NeedsYard = DetectNeedsYard(query),
            GoodWithChildren = DetectChildrenPreference(query),
            GoodWithPets = DetectPetPreference(query),
            ExperienceLevel = DetectExperienceLevel(query),
            DesiredTraits = mustHave.Concat(niceToHave).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MustHave = mustHave,
            NiceToHave = niceToHave,
            AvoidTraits = avoid,
            Avoid = avoid,
            EvidenceToLookFor = DetectEvidenceToLookFor(query, compatibility),
            DisplayChipIntent = BuildDisplayChipIntent(primaryIntent, compatibilityTarget, homeType, activityLevel, sizes, statuses),
            Count = 16,
            Limit = 16
        };
    }

    private static void MergeDeterministicConstraints(AdoptionCopilotSearchDogsArgs target, AdoptionCopilotSearchDogsArgs deterministic)
    {
        target.Query = string.IsNullOrWhiteSpace(target.Query) ? deterministic.Query : target.Query;
        target.PrimaryIntent ??= deterministic.PrimaryIntent;
        target.CompatibilityTarget ??= deterministic.CompatibilityTarget;
        if (deterministic.Sizes?.Count > 0)
        {
            target.Sizes = deterministic.Sizes;
        }

        if (deterministic.Statuses?.Count > 0)
        {
            target.Statuses = deterministic.Statuses;
        }

        if (!string.IsNullOrWhiteSpace(deterministic.Neighborhood))
        {
            target.Neighborhood = deterministic.Neighborhood;
        }

        if (!string.IsNullOrWhiteSpace(deterministic.City))
        {
            target.City = deterministic.City;
        }

        if (deterministic.MaxAgeYears is > 0)
        {
            target.MaxAgeYears = deterministic.MaxAgeYears;
            target.AgeComparison = deterministic.AgeComparison;
        }

        if (deterministic.MinAgeYears is > 0)
        {
            target.MinAgeYears = deterministic.MinAgeYears;
            target.AgeComparison = deterministic.AgeComparison;
        }

        if (deterministic.BehaviorTerms?.Count > 0)
        {
            target.BehaviorTerms = MergeListValues(target.BehaviorTerms, deterministic.BehaviorTerms);
        }

        if (deterministic.TemperamentTags?.Count > 0)
        {
            target.TemperamentTags = MergeListValues(target.TemperamentTags, deterministic.TemperamentTags);
        }

        if (deterministic.Temperaments?.Count > 0)
        {
            target.Temperaments = MergeListValues(target.Temperaments, deterministic.Temperaments);
        }

        if (deterministic.Compatibility?.Count > 0)
        {
            target.Compatibility = MergeListValues(target.Compatibility, deterministic.Compatibility);
        }

        if (deterministic.MustHave?.Count > 0)
        {
            target.MustHave = MergeListValues(target.MustHave, deterministic.MustHave);
        }

        if (deterministic.NiceToHave?.Count > 0)
        {
            target.NiceToHave = MergeListValues(target.NiceToHave, deterministic.NiceToHave);
        }

        if (deterministic.Avoid?.Count > 0)
        {
            target.Avoid = MergeListValues(target.Avoid, deterministic.Avoid);
        }

        if (deterministic.DesiredTraits?.Count > 0)
        {
            target.DesiredTraits = MergeListValues(target.DesiredTraits, deterministic.DesiredTraits);
        }

        if (deterministic.AvoidTraits?.Count > 0)
        {
            target.AvoidTraits = MergeListValues(target.AvoidTraits, deterministic.AvoidTraits);
        }

        if (deterministic.EvidenceToLookFor?.Count > 0)
        {
            target.EvidenceToLookFor = MergeListValues(target.EvidenceToLookFor, deterministic.EvidenceToLookFor);
        }

        if (deterministic.DisplayChipIntent?.Count > 0)
        {
            target.DisplayChipIntent = MergeListValues(target.DisplayChipIntent, deterministic.DisplayChipIntent);
        }

        target.EnergyLevel ??= deterministic.EnergyLevel;
        target.ActivityLevel ??= deterministic.ActivityLevel;
        target.HomeType ??= deterministic.HomeType;
        target.HousingPreference ??= deterministic.HousingPreference;
        target.ApartmentFriendly ??= deterministic.ApartmentFriendly;
        target.YardFriendly ??= deterministic.YardFriendly;
        target.YardRequired ??= deterministic.YardRequired;
        target.NeedsYard ??= deterministic.NeedsYard;
        target.GoodWithChildren ??= deterministic.GoodWithChildren;
        target.GoodWithPets ??= deterministic.GoodWithPets;
        target.ExperienceLevel ??= deterministic.ExperienceLevel;
        target.Limit ??= deterministic.Limit;
        target.Count = Math.Clamp(target.Count <= 0 ? deterministic.Count : target.Count, 1, 20);
    }

    private static List<string> MergeListValues(List<string>? target, IEnumerable<string> deterministic)
    {
        return (target ?? [])
            .Concat(deterministic)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AdoptionCopilotResponse BuildFallbackResponse(
        string query,
        AdoptionCopilotToolSearchResult search,
        string? fallbackReason)
    {
        return BuildFallbackFromCandidates(query, search.Dogs, search.UsedSemanticSearch, false, search.AppliedConstraints, fallbackReason, search.EmptyReason);
    }

    private static AdoptionCopilotResponse BuildFallbackFromCandidates(
        string query,
        IReadOnlyList<AdoptionCopilotToolDogCandidate> candidates,
        bool usedSemanticSearch,
        bool usedToolCalling,
        IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints,
        string? fallbackReason,
        string? emptyReason = null)
    {
        var results = candidates
            .OrderByDescending(candidate => candidate.ScorePercent)
            .Take(6)
            .Select(candidate => BuildFallbackDogResult(candidate, appliedConstraints))
            .ToList();

        return new AdoptionCopilotResponse(
            results.Count == 0
                ? BuildNoResultsMessage(emptyReason)
                : BuildAssistantMessage(query, results.FirstOrDefault()?.Dog.Name, appliedConstraints),
            results,
            false,
            usedSemanticSearch,
            usedToolCalling,
            fallbackReason,
            appliedConstraints);
    }

    private static string BuildNoResultsMessage(string? emptyReason)
    {
        if (!string.IsNullOrWhiteSpace(emptyReason) &&
            emptyReason.StartsWith("No dogs found in ", StringComparison.OrdinalIgnoreCase))
        {
            return $"{emptyReason} Try nearby search or a larger area.";
        }

        return string.IsNullOrWhiteSpace(emptyReason)
            ? "No dogs matched your exact request. Try broadening one requirement or browsing all dogs."
            : $"{emptyReason} Try broadening one requirement or browsing all dogs.";
    }

    private static string NormalizeAssistantMessage(
        string? assistantMessage,
        string query,
        string? topDogName,
        IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints)
    {
        if (string.IsNullOrWhiteSpace(assistantMessage))
        {
            return BuildAssistantMessage(query, topDogName, appliedConstraints);
        }

        var normalized = assistantMessage.Trim();
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["from the dogs you shared"] = "from the available PawConnect dogs",
            ["from dogs you shared"] = "from available PawConnect dogs",
            ["from the dogs you provided"] = "from the available PawConnect dogs",
            ["from dogs you provided"] = "from available PawConnect dogs",
            ["from the provided dogs"] = "from the available PawConnect dogs",
            ["from provided dogs"] = "from available PawConnect dogs",
            ["from the candidate dogs"] = "from the available PawConnect dogs",
            ["from candidate dogs"] = "from available PawConnect dogs",
            ["database"] = "PawConnect"
        };

        foreach (var replacement in replacements)
        {
            normalized = normalized.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
        }

        return normalized;
    }

    private static string BuildAssistantMessage(
        string query,
        string? topDogName,
        IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints)
    {
        if (HasAppliedConstraint(appliedConstraints, "Compatibility", "Sensitive dog"))
        {
            return "These dogs seem more suitable for a home with a sick or recovering dog because they show calmer compatibility signals. Please confirm introductions and fit with the shelter.";
        }

        if (HasAppliedConstraint(appliedConstraints, "Compatibility", "Senior dog"))
        {
            return "These dogs seem more suitable for a home with an older dog because they show calmer dog-to-dog signals. Please confirm introductions and fit with the shelter.";
        }

        return string.IsNullOrWhiteSpace(topDogName)
            ? "These dogs are the closest PawConnect matches. Review each profile before sending a request."
            : $"{topDogName} looks like the strongest match. Review the profile and shelter details before sending a request.";
    }

    private static bool HasAppliedConstraint(
        IReadOnlyList<AdoptionCopilotConstraint> constraints,
        string label,
        string value)
    {
        return constraints.Any(constraint =>
            constraint.Label.Equals(label, StringComparison.OrdinalIgnoreCase) &&
            constraint.Value.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static AdoptionCopilotDogResult BuildFallbackDogResult(
        AdoptionCopilotToolDogCandidate result,
        IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints)
    {
        var matchedCriteria = BuildMatchedCriteria(result.Dog, result.DistanceKm, appliedConstraints);
        return new AdoptionCopilotDogResult(
            result.DogId,
            result.Dog,
            result.ScorePercent,
            result.MatchLabel,
            PolishReasons(result.SafeReasons, matchedCriteria).Take(3).ToList(),
            result.SuggestedNextAction,
            result.DistanceKm,
            false,
            matchedCriteria,
            result.DisplayTags ?? [],
            result.CautionTags ?? []);
    }

    private static AdoptionCopilotDogResult BuildAiResult(
        OpenAiAdoptionCopilotItem aiResult,
        AdoptionCopilotToolDogCandidate searchResult,
        IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints)
    {
        var matchedCriteria = BuildMatchedCriteria(searchResult.Dog, searchResult.DistanceKm, appliedConstraints);
        var reasons = ChooseTrustedReasons(aiResult.Reasons, searchResult.SafeReasons, searchResult.Dog);
        var displayTags = ChooseTrustedTags(aiResult.DisplayTags, searchResult.DisplayTags);
        var cautionTags = ChooseTrustedTags(aiResult.CautionTags, searchResult.CautionTags);

        var safeScore = Math.Clamp(Math.Min(Math.Clamp(aiResult.ScorePercent, 45, 96), searchResult.ScorePercent + 4), 45, 96);
        if (searchResult.Dog.Status == DogStatus.Reserved)
        {
            safeScore = Math.Min(safeScore, 89);
        }

        if (HasUncertainPrimaryEvidence(searchResult))
        {
            safeScore = Math.Min(safeScore, 80);
        }

        return new AdoptionCopilotDogResult(
            searchResult.DogId,
            searchResult.Dog,
            safeScore,
            GetMatchLabel(safeScore),
            PolishReasons(reasons, matchedCriteria).Take(3).ToList(),
            string.IsNullOrWhiteSpace(aiResult.SuggestedNextAction)
                ? searchResult.SuggestedNextAction
                : aiResult.SuggestedNextAction,
            searchResult.DistanceKm,
            true,
            matchedCriteria,
            displayTags,
            cautionTags);
    }

    private static bool HasUncertainPrimaryEvidence(AdoptionCopilotToolDogCandidate result)
    {
        return (result.DisplayTags ?? []).Any(tag =>
                tag.StartsWith("Ask shelter", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith("No ", StringComparison.OrdinalIgnoreCase)) ||
            (result.MissingEvidence ?? []).Count > 0;
    }

    private static IReadOnlyList<string> ChooseTrustedTags(
        IReadOnlyList<string>? proposedTags,
        IReadOnlyList<string>? supportedTags)
    {
        var supported = supportedTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (proposedTags is null || proposedTags.Count == 0)
        {
            return supported;
        }

        var proposed = proposedTags
            .Where(tag => supported.Contains(tag.Trim(), StringComparer.OrdinalIgnoreCase))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return proposed.Count > 0 ? proposed : supported;
    }

    private static IReadOnlyList<AdoptionCopilotConstraint> BuildMatchedCriteria(
        Dog dog,
        double? distanceKm,
        IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints)
    {
        var criteria = new List<AdoptionCopilotConstraint>();
        foreach (var constraint in appliedConstraints)
        {
            var value = constraint.Label switch
            {
                "Age" => FormatDogAgeCriterion(dog),
                "Size" => dog.Size.ToString(),
                "Neighborhood" => EmptyToNull(dog.Shelter?.Neighborhood),
                "Status" => dog.Status.ToString(),
                "Breed" => dog.Breed,
                "City" => EmptyToNull(dog.Shelter?.City) ?? EmptyToNull(dog.Location),
                "Shelter" => EmptyToNull(dog.Shelter?.Name),
                "Near" => distanceKm.HasValue ? $"{distanceKm.Value:0.#} km away" : null,
                "Location" => EmptyToNull(dog.Shelter?.Neighborhood) ?? EmptyToNull(dog.Shelter?.City) ?? EmptyToNull(dog.Location),
                "Temperament" => GetMatchedBehaviorValue(dog, constraint.Value),
                "Lifestyle" => GetMatchedLifestyleValue(dog, constraint.Value),
                "Home" => GetMatchedHomeValue(dog, constraint.Value),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value) &&
                !criteria.Any(existing => string.Equals(existing.Label, constraint.Label, StringComparison.OrdinalIgnoreCase)))
            {
                criteria.Add(new AdoptionCopilotConstraint(constraint.Label, value));
            }
        }

        return criteria.Take(4).ToList();
    }

    private static IReadOnlyList<string> ChooseTrustedReasons(
        IReadOnlyList<string> aiReasons,
        IReadOnlyList<string> safeReasons,
        Dog dog)
    {
        if (aiReasons.Count == 0)
        {
            return safeReasons.Take(3).ToList();
        }

        var trusted = aiReasons
            .Where(reason => IsSupportedReason(reason, safeReasons, dog))
            .ToList();

        return (trusted.Count > 0 ? trusted : safeReasons)
            .Concat(safeReasons)
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static bool IsSupportedReason(string reason, IReadOnlyList<string> safeReasons, Dog dog)
    {
        var category = GetReasonCategory(reason);
        if (category is null)
        {
            return false;
        }

        if (category == "reserved")
        {
            return dog.Status == DogStatus.Reserved;
        }

        return safeReasons
            .Select(GetReasonCategory)
            .Where(safeCategory => safeCategory is not null)
            .Any(safeCategory => AreCompatibleReasonCategories(category, safeCategory!));
    }

    private static bool AreCompatibleReasonCategories(string first, string second)
    {
        if (first == second)
        {
            return true;
        }

        string[] activeOutdoor = ["active", "yard", "outdoor", "long-walks"];
        return activeOutdoor.Contains(first) && activeOutdoor.Contains(second);
    }

    private static string? GetReasonCategory(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var lower = reason.ToLowerInvariant();
        if (lower.Contains("reserved"))
        {
            return "reserved";
        }

        if (lower.Contains("short walk") || lower.Contains("slow walk"))
        {
            return "short-walks";
        }

        if (lower.Contains("indoor"))
        {
            return "indoor-rest";
        }

        if (lower.Contains("settles"))
        {
            return "settles";
        }

        if (lower.Contains("quiet") || lower.Contains("predictable"))
        {
            return "quiet";
        }

        if (lower.Contains("apartment"))
        {
            return "apartment";
        }

        if (lower.Contains("yard"))
        {
            return "yard";
        }

        if (lower.Contains("outdoor") || lower.Contains("fetch") || lower.Contains("space to run"))
        {
            return "outdoor";
        }

        if (lower.Contains("longer walk") || lower.Contains("brisk walk") || lower.Contains("hiking"))
        {
            return "long-walks";
        }

        if (lower.Contains("active") || lower.Contains("energetic") || lower.Contains("playful"))
        {
            return "active";
        }

        if (lower.Contains("friendly") || lower.Contains("social"))
        {
            return "friendly";
        }

        if (lower.Contains("small size") || lower.Contains("medium size") || lower.Contains("large size") || lower.Contains("size"))
        {
            return "size";
        }

        if (lower.Contains("age"))
        {
            return "age";
        }

        if (lower.Contains("children") || lower.Contains("family"))
        {
            return "children";
        }

        if (lower.Contains("pet") || lower.Contains("cat") || lower.Contains("good with dogs"))
        {
            return "pets";
        }

        return null;
    }

    private static IReadOnlyList<string> PolishReasons(
        IReadOnlyList<string> reasons,
        IReadOnlyList<AdoptionCopilotConstraint> matchedCriteria)
    {
        var polished = new List<string>();
        polished.AddRange(reasons.Select(PolishReason));
        return polished
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string PolishReason(string reason)
    {
        var normalized = reason.Trim();
        return normalized.Equals("Natural-language match for your search.", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Matches your search", StringComparison.OrdinalIgnoreCase)
            ? "Relevant profile match"
            : normalized;
    }

    private static string FormatDogAgeCriterion(Dog dog)
    {
        return DogAgeFormatter.Format(dog).Replace(" old", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetMatchedBehaviorValue(Dog dog, string requestedBehavior)
    {
        var profile = $"{dog.BehaviorDescription} {dog.Description}";
        var terms = requestedBehavior.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matched = terms.FirstOrDefault(term => profile.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matched))
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(matched.ToLowerInvariant());
        }

        return EmptyToNull(requestedBehavior);
    }

    private static string? GetMatchedLifestyleValue(Dog dog, string requestedLifestyle)
    {
        var profile = $"{dog.BehaviorDescription} {dog.Description}";
        if (requestedLifestyle.Contains("Low", StringComparison.OrdinalIgnoreCase) ||
            requestedLifestyle.Contains("low activity", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(profile.ToLowerInvariant(), ["short walks", "indoor rest", "quiet", "settles", "relaxed", "calm", "gentle"])
                ? "Low activity fit"
                : null;
        }

        if (requestedLifestyle.Contains("Active", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(profile.ToLowerInvariant(), ["active", "energetic", "outdoor", "yard", "longer walks", "playful"])
                ? "Active fit"
                : null;
        }

        return null;
    }

    private static string? GetMatchedHomeValue(Dog dog, string requestedHome)
    {
        if (requestedHome.Contains("Apartment", StringComparison.OrdinalIgnoreCase))
        {
            return dog.Size is DogSize.Small or DogSize.Medium ? dog.Size.ToString() : null;
        }

        if (requestedHome.Contains("yard", StringComparison.OrdinalIgnoreCase) ||
            requestedHome.Contains("house", StringComparison.OrdinalIgnoreCase))
        {
            return dog.Size is DogSize.Medium or DogSize.Large ? dog.Size.ToString() : null;
        }

        return null;
    }

    private static AdoptionCopilotDogToolDto ToDogDto(AdoptionCopilotToolDogCandidate candidate)
    {
        var dog = candidate.Dog;
        return new AdoptionCopilotDogToolDto(
            dog.Id,
            dog.Name,
            dog.Breed,
            DogAgeFormatter.Format(dog),
            dog.Size.ToString(),
            dog.Status.ToString(),
            EmptyToNull(dog.Description),
            EmptyToNull(dog.BehaviorDescription),
            EmptyToNull(dog.Shelter?.Name),
            EmptyToNull(dog.Shelter?.City),
            EmptyToNull(dog.Shelter?.Neighborhood),
            candidate.DistanceKm,
            dog.Images
                .Where(image => !string.IsNullOrWhiteSpace(image.ImageUrl))
                .OrderByDescending(image => image.IsMainImage)
                .ThenBy(image => image.Id)
                .Select(image => image.ImageUrl)
                .FirstOrDefault(),
            candidate.SafeReasons,
            candidate.DisplayTags ?? [],
            candidate.CautionTags ?? [],
            candidate.PositiveEvidence ?? [],
            candidate.CautionEvidence ?? [],
            candidate.NegativeEvidence ?? [],
            candidate.MissingEvidence ?? [],
            candidate.EvidenceSummary,
            candidate.ScorePercent,
            candidate.MatchLabel);
    }

    private async Task<string?> DetectExplicitNeighborhoodAsync(string query, CancellationToken cancellationToken)
    {
        var neighborhoods = await context.Shelters
            .AsNoTracking()
            .Select(shelter => shelter.Neighborhood)
            .Where(neighborhood => neighborhood != null && neighborhood != "")
            .Distinct()
            .ToListAsync(cancellationToken);

        var knownNeighborhoods = neighborhoods
            .Concat(KnownClujNeighborhoods)
            .Where(neighborhood => !string.IsNullOrWhiteSpace(neighborhood))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(neighborhood => neighborhood!.Length)
            .ToList();

        var normalizedQuery = NormalizeForNeighborhoodMatch(query);
        foreach (var neighborhood in knownNeighborhoods)
        {
            var normalizedNeighborhood = NormalizeForNeighborhoodMatch(neighborhood!);
            if (ContainsExplicitNeighborhoodPhrase(normalizedQuery, normalizedNeighborhood))
            {
                return neighborhood;
            }
        }

        return null;
    }

    private static bool ContainsExplicitNeighborhoodPhrase(string normalizedQuery, string normalizedNeighborhood)
    {
        string[] prefixes =
        [
            "in ",
            "near ",
            "around ",
            "within ",
            "langa ",
            "aproape de ",
            "in cartierul ",
            "cartierul "
        ];

        return prefixes.Any(prefix => normalizedQuery.Contains($"{prefix}{normalizedNeighborhood}", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeForNeighborhoodMatch(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> DetectSizes(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        if (normalized.Contains("any size", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("all sizes", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var sizes = new List<string>();
        AddSizeIfPresent(sizes, normalized, "small", DogSize.Small);
        AddSizeIfPresent(sizes, normalized, "medium", DogSize.Medium);
        AddSizeIfPresent(sizes, normalized, "large", DogSize.Large);
        return sizes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DetectStatuses(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var statuses = new List<string>();

        if (tokens.Contains("available"))
        {
            statuses.Add(DogStatus.Available.ToString());
        }

        if (tokens.Contains("reserved"))
        {
            statuses.Add(DogStatus.Reserved.ToString());
        }

        if (tokens.Contains("adopted"))
        {
            statuses.Add(DogStatus.Adopted.ToString());
        }

        if (normalized.Contains("in treatment", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("treatment", StringComparison.OrdinalIgnoreCase))
        {
            statuses.Add(DogStatus.InTreatment.ToString());
        }

        return statuses.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static DetectedAgeConstraint DetectAgeConstraint(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var strictPatterns = new[]
        {
            @"\b(?:under|younger than|less than|below)\s+(\d{1,2})\s*(?:year|years|yr|yrs)?\b",
            @"\b(\d{1,2})\s*(?:year|years|yr|yrs)\s*(?:old\s*)?(?:and under|or less)\b"
        };

        foreach (var pattern in strictPatterns)
        {
            if (TryMatchAgeYears(normalized, pattern, out var years))
            {
                return new DetectedAgeConstraint(years, null, "Under");
            }
        }

        if (!HasExistingDogContext(normalized) &&
            (normalized.Contains("puppy", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("puppies", StringComparison.OrdinalIgnoreCase)))
        {
            return new DetectedAgeConstraint(1, null, "Under");
        }

        var inclusivePatterns = new[]
        {
            @"\b(?:max(?:imum)? age|max age|max|up to)\s+(\d{1,2})\s*(?:year|years|yr|yrs)?\b",
            @"\b(\d{1,2})\s*(?:year|years|yr|yrs)\s*(?:old\s*)?(?:or younger|max)\b"
        };

        foreach (var pattern in inclusivePatterns)
        {
            if (TryMatchAgeYears(normalized, pattern, out var years))
            {
                return new DetectedAgeConstraint(years, null, "Max");
            }
        }

        if (!HasExistingDogContext(normalized) &&
            (normalized.Contains("young dog", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("young dogs", StringComparison.OrdinalIgnoreCase)))
        {
            return new DetectedAgeConstraint(2, null, "Max");
        }

        var minimumPatterns = new[]
        {
            @"\b(?:older than|over|at least)\s+(\d{1,2})\s*(?:year|years|yr|yrs)?\b",
            @"\b(\d{1,2})\s*(?:year|years|yr|yrs)\s*(?:old\s*)?(?:or older|and older|plus)\b"
        };

        foreach (var pattern in minimumPatterns)
        {
            if (TryMatchAgeYears(normalized, pattern, out var years))
            {
                return new DetectedAgeConstraint(null, years, "AtLeast");
            }
        }

        if (!HasExistingDogContext(normalized) &&
            (normalized.Contains("senior dog", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("senior dogs", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("older dog", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("older dogs", StringComparison.OrdinalIgnoreCase)))
        {
            return new DetectedAgeConstraint(null, 7, "AtLeast");
        }

        return new DetectedAgeConstraint(null, null, null);
    }

    private static bool TryMatchAgeYears(string normalizedQuery, string pattern, out int years)
    {
        years = 0;
        var match = Regex.Match(normalizedQuery, pattern, RegexOptions.IgnoreCase);
        return match.Success &&
            int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out years) &&
            years > 0;
    }

    private static void AddSizeIfPresent(List<string> sizes, string normalizedQuery, string token, DogSize size)
    {
        if (normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(token))
        {
            sizes.Add(size.ToString());
        }
    }

    private static List<string> DetectBehaviorTerms(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var terms = new List<string>();
        AddTermIfAny(terms, "calm", normalized, "calm", "quiet", "gentle", "relaxed", "easy going", "low energy");
        AddTermIfAny(terms, "friendly", normalized, "friendly", "social", "affectionate", "good with people");
        AddTermIfAny(terms, "playful", normalized, "playful");
        AddTermIfAny(terms, "beginner", normalized, "beginner", "first time", "easy dog", "easygoing");
        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DetectTemperamentTags(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var tags = new List<string>();
        AddTermIfAny(tags, "Calm", normalized, "calm", "quiet", "gentle", "relaxed", "low energy");
        AddTermIfAny(tags, "Friendly", normalized, "friendly", "social", "affectionate", "good with people");
        AddTermIfAny(tags, "Playful", normalized, "playful");
        AddTermIfAny(tags, "Shy", normalized, "shy", "cautious", "anxious");
        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DetectCompatibility(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var values = new List<string>();
        AddTermIfAny(values, "OlderChildren", normalized, "older children", "older kids");
        if (!values.Any(value => value.Equals("OlderChildren", StringComparison.OrdinalIgnoreCase)))
        {
            AddTermIfAny(values, "Children", normalized, "kids", "children", "family");
        }

        AddTermIfAny(values, "Cats", normalized, "cat", "cats");
        AddTermIfAny(values, "SmallAnimals", normalized, "small animal", "small animals", "rabbit", "rabbits");
        AddTermIfAny(values, "OtherDogs", normalized, "other dogs", "good with dogs", "another dog");
        if (HasHouseholdDogContext(normalized))
        {
            values.Add("OtherDogs");
            AddTermIfAny(values, "SeniorDog", normalized, "older dog", "old dog", "senior dog", "elderly dog");
            AddTermIfAny(values, "SickDog", normalized, "sick dog", "ill dog", "recovering dog", "medical needs", "health issues");
            AddTermIfAny(values, "AnxiousDog", normalized, "anxious dog", "nervous dog", "shy dog", "fearful dog");
            AddTermIfAny(values, "YoungDog", normalized, "young dog", "puppy", "puppies", "energetic dog", "playful dog");
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string DetectPrimaryIntent(
        string query,
        IReadOnlyCollection<string> compatibility,
        string? homeType,
        string? activityLevel,
        IReadOnlyCollection<string> sizes)
    {
        if (compatibility.Count > 0)
        {
            return "Compatibility";
        }

        if (!string.IsNullOrWhiteSpace(homeType))
        {
            return "HomeSuitability";
        }

        if (!string.IsNullOrWhiteSpace(activityLevel))
        {
            return "ActivityLevel";
        }

        var normalized = NormalizeForNeighborhoodMatch(query);
        if (ContainsAny(normalized, ["calm", "quiet", "gentle", "friendly", "playful", "shy", "anxious"]))
        {
            return "Temperament";
        }

        if (ContainsAny(normalized, ["beginner", "first time", "first dog", "experienced"]))
        {
            return "ExperienceLevel";
        }

        if (sizes.Count > 0)
        {
            return "Size";
        }

        if (ContainsAny(normalized, ["near", "around", "in ", "langa", "aproape"]))
        {
            return "Location";
        }

        return "Temperament";
    }

    private static string? DetectCompatibilityTarget(IReadOnlyCollection<string> compatibility)
    {
        if (compatibility.Any(value => value.Equals("SeniorDog", StringComparison.OrdinalIgnoreCase)))
        {
            return "SeniorDog";
        }

        if (compatibility.Any(value => value.Equals("SickDog", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("AnxiousDog", StringComparison.OrdinalIgnoreCase)))
        {
            return "SensitiveDog";
        }

        if (compatibility.Any(value => value.Equals("YoungDog", StringComparison.OrdinalIgnoreCase)))
        {
            return "YoungDog";
        }

        if (compatibility.Any(value => value.Equals("Cats", StringComparison.OrdinalIgnoreCase)))
        {
            return "Cats";
        }

        if (compatibility.Any(value => value.Equals("SmallAnimals", StringComparison.OrdinalIgnoreCase)))
        {
            return "SmallAnimals";
        }

        if (compatibility.Any(value => value.Equals("OlderChildren", StringComparison.OrdinalIgnoreCase)))
        {
            return "OlderChildren";
        }

        if (compatibility.Any(value => value.Equals("Children", StringComparison.OrdinalIgnoreCase)))
        {
            return "Children";
        }

        return compatibility.Any(value => value.Equals("OtherDogs", StringComparison.OrdinalIgnoreCase))
            ? "OtherDogs"
            : null;
    }

    private static bool HasExistingDogContext(string normalizedQuery)
    {
        return ContainsAny(normalizedQuery,
            [
                "i have",
                "we have",
                "my dog",
                "our dog",
                "already have",
                "current dog",
                "existing dog",
                "at home"
            ]);
    }

    private static bool HasHouseholdDogContext(string normalizedQuery)
    {
        return ContainsAny(normalizedQuery,
        [
            "my dog",
            "our dog",
            "another dog",
            "current dog",
            "existing dog",
            "dog at home",
            "dogs at home",
            "already have a dog",
            "already have dogs",
            "i have a dog",
            "i have an older dog",
            "i have a senior dog",
            "i have a sick dog",
            "i have a recovering dog",
            "we have a dog",
            "older dog",
            "old dog",
            "senior dog",
            "elderly dog",
            "sick dog",
            "ill dog",
            "recovering dog",
            "dog recovering",
            "anxious dog",
            "nervous dog",
            "shy dog",
            "young dog",
            "puppy",
            "puppies"
        ]);
    }

    private static string? DetectExperienceLevel(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        if (ContainsAny(normalized, ["beginner", "first time", "first-time", "first dog"]))
        {
            return "Beginner";
        }

        if (ContainsAny(normalized, ["experienced", "previous dog experience"]))
        {
            return "Experienced";
        }

        return null;
    }

    private static List<string> DetectMustHaveSignals(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var values = new List<string>();
        AddTermIfAny(values, "short walks", normalized, "short walks", "slow walks");
        AddTermIfAny(values, "indoor rest", normalized, "indoor rest", "quiet indoors");
        AddTermIfAny(values, "quiet routine", normalized, "quiet", "not too much activity", "does not need much activity", "doesn t need much activity");
        AddTermIfAny(values, "outdoor play", normalized, "outdoor play", "yard", "garden");
        AddTermIfAny(values, "calm around cats", normalized, "cat", "cats");
        if (ContainsAny(normalized, ["cat", "cats"]))
        {
            values.Add("redirectable around cats");
            values.Add("slow introductions");
        }

        AddTermIfAny(values, "older children", normalized, "older children", "older kids");
        if (HasExistingDogContext(normalized) &&
            ContainsAny(normalized, ["older dog", "old dog", "senior dog", "elderly dog", "sick dog", "ill dog", "recovering dog", "anxious dog", "nervous dog", "shy dog"]))
        {
            values.Add("calm dog company");
            values.Add("quiet routine");
        }

        if (HasExistingDogContext(normalized) &&
            ContainsAny(normalized, ["young dog", "puppy", "puppies", "energetic dog", "playful dog"]))
        {
            values.Add("playful dog friends");
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DetectNiceToHaveSignals(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var values = new List<string>();
        AddTermIfAny(values, "settles quickly", normalized, "settles", "settle");
        AddTermIfAny(values, "gentle handling", normalized, "gentle", "beginner", "first time", "children", "kids");
        AddTermIfAny(values, "slow introductions", normalized, "cat", "cats", "other dogs", "another dog");
        AddTermIfAny(values, "training games", normalized, "active", "yard", "garden", "house");
        if (HasExistingDogContext(normalized) &&
            ContainsAny(normalized, ["older dog", "old dog", "senior dog", "elderly dog", "sick dog", "ill dog", "recovering dog", "anxious dog", "nervous dog", "shy dog"]))
        {
            values.Add("slow dog introductions");
            values.Add("gentle handling");
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DetectAvoidSignals(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var values = new List<string>();
        if (ContainsAny(normalized, ["apartment", "flat", "low activity", "not too active", "does not need much activity", "doesn t need much activity"]))
        {
            values.Add("very energetic");
            values.Add("needs lots of outdoor space");
        }

        if (ContainsAny(normalized, ["cat", "cats"]))
        {
            values.Add("chase behavior");
            values.Add("too interested in fast-moving small animals");
        }

        if (ContainsAny(normalized, ["children", "kids", "family"]))
        {
            values.Add("not suitable for young/noisy children");
        }

        if (HasExistingDogContext(normalized) &&
            ContainsAny(normalized, ["older dog", "old dog", "senior dog", "elderly dog", "sick dog", "ill dog", "recovering dog", "anxious dog", "nervous dog", "shy dog"]))
        {
            values.Add("very energetic");
            values.Add("pushy dogs");
            values.Add("bouncy playmates");
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DetectEvidenceToLookFor(string query, IReadOnlyCollection<string> compatibility)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        var values = new List<string>();
        if (compatibility.Any(value => value.Equals("Cats", StringComparison.OrdinalIgnoreCase)))
        {
            values.AddRange(["shelter cats", "calm near cats", "notices cats but redirects", "slow introductions to small animals"]);
        }

        if (compatibility.Any(value => value.Equals("SeniorDog", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("SickDog", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("AnxiousDog", StringComparison.OrdinalIgnoreCase)))
        {
            values.AddRange(["calm canine company", "walks politely near dogs", "prefers steady dogs", "slow introductions", "not suited for bouncy dogs"]);
        }

        if (compatibility.Any(value => value.Equals("Children", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("OlderChildren", StringComparison.OrdinalIgnoreCase)))
        {
            values.AddRange(["supervised visits with children", "older children", "gentle treat taking", "calm family routine"]);
        }

        if (ContainsAny(normalized, ["apartment", "flat", "low activity", "not too much activity", "short walks"]))
        {
            values.AddRange(["short walks", "indoor rest", "settles quickly", "quiet routine"]);
        }

        if (ContainsAny(normalized, ["active", "yard", "garden", "house with a yard"]))
        {
            values.AddRange(["outdoor play", "longer walks", "training games", "space to run"]);
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> BuildDisplayChipIntent(
        string primaryIntent,
        string? compatibilityTarget,
        string? homeType,
        string? activityLevel,
        IReadOnlyCollection<string> sizes,
        IReadOnlyCollection<string> statuses)
    {
        var values = new List<string> { primaryIntent };
        if (!string.IsNullOrWhiteSpace(compatibilityTarget))
        {
            values.Add(compatibilityTarget);
        }

        if (!string.IsNullOrWhiteSpace(homeType))
        {
            values.Add(homeType);
        }

        if (!string.IsNullOrWhiteSpace(activityLevel))
        {
            values.Add(activityLevel);
        }

        values.AddRange(sizes);
        values.AddRange(statuses);
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddTermIfAny(List<string> terms, string canonical, string normalizedQuery, params string[] variants)
    {
        if (variants.Any(variant => normalizedQuery.Contains(variant, StringComparison.OrdinalIgnoreCase)))
        {
            terms.Add(canonical);
        }
    }

    private static string? DetectEnergyLevel(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        if (ContainsAny(normalized, ["active", "energetic", "playful", "running", "hiking", "high energy", "long walks", "outdoor play"]))
        {
            return "High";
        }

        if (ContainsAny(normalized,
            [
                "calm",
                "quiet",
                "gentle",
                "relaxed",
                "low energy",
                "low activity",
                "less activity",
                "not too active",
                "doesn t need too much activity",
                "does not need too much activity",
                "doesn t need much activity",
                "does not need much activity",
                "doesn t need too much exercise",
                "does not need too much exercise",
                "not much exercise",
                "short walks",
                "slow walks"
            ]))
        {
            return "Low";
        }

        return null;
    }

    private static string? DetectHouseholdDogActivityLevel(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        if (!HasExistingDogContext(normalized))
        {
            return null;
        }

        if (ContainsAny(normalized, ["older dog", "old dog", "senior dog", "elderly dog", "sick dog", "ill dog", "recovering dog", "anxious dog", "nervous dog", "shy dog"]))
        {
            return "Low";
        }

        if (ContainsAny(normalized, ["young dog", "puppy", "puppies", "energetic dog", "playful dog"]))
        {
            return "Medium";
        }

        return null;
    }

    private static string? DetectHomeType(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        if (ContainsAny(normalized, ["apartment", "flat"]))
        {
            return "Apartment";
        }

        if (ContainsAny(normalized, ["house", "yard", "garden"]))
        {
            return "House with yard";
        }

        return null;
    }

    private static string? DetectCity(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        if (ContainsAny(normalized, ["cluj napoca", "cluj-napoca", "cluj"]))
        {
            return "Cluj";
        }

        if (ContainsAny(normalized, ["bucharest", "bucuresti"]))
        {
            return "Bucharest";
        }

        return null;
    }

    private static string? DetectHousingPreference(string query)
    {
        return DetectHomeType(query);
    }

    private static bool? DetectApartmentFriendly(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        return ContainsAny(normalized, ["apartment", "flat"]) ? true : null;
    }

    private static bool? DetectYardFriendly(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        return ContainsAny(normalized, ["yard", "garden", "house"]) ? true : null;
    }

    private static bool? DetectYardRequired(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        return ContainsAny(normalized, ["needs yard", "need a yard", "requires yard", "must have yard"]) ? true : null;
    }

    private static bool? DetectNeedsYard(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        return ContainsAny(normalized, ["yard", "garden"])
            ? true
            : null;
    }

    private static bool? DetectChildrenPreference(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        return ContainsAny(normalized, ["kids", "children", "family"]) ? true : null;
    }

    private static bool? DetectPetPreference(string query)
    {
        var normalized = NormalizeForNeighborhoodMatch(query);
        return ContainsAny(normalized, ["cat", "cats", "with other dogs", "good with dogs", "another dog", "other pets", "pets"]) ? true : null;
    }

    private static bool ContainsAny(string value, IEnumerable<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasUnresolvedNeighborhoodIntent(string query, string? detectedNeighborhood)
    {
        if (!string.IsNullOrWhiteSpace(detectedNeighborhood))
        {
            return false;
        }

        var normalized = NormalizeForNeighborhoodMatch(query);
        return normalized.Contains("neighborhood", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("cartier", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<AdoptionCopilotConstraint> BuildDeterministicConstraintPreview(AdoptionCopilotSearchDogsArgs args)
    {
        var constraints = new List<AdoptionCopilotConstraint>();
        AddListConstraint("Size", args.Sizes);
        AddListConstraint("Status", args.Statuses);
        var ageConstraint = FormatAgeConstraint(args);
        if (!string.IsNullOrWhiteSpace(ageConstraint))
        {
            constraints.Add(new AdoptionCopilotConstraint("Age", ageConstraint));
        }

        AddListConstraint("Location", SplitSingle(args.Neighborhood).Concat(SplitSingle(args.City)));
        AddListConstraint("Temperament", NormalizeTemperamentValues(MergeListValues(args.Temperaments, (args.TemperamentTags ?? []).Concat(args.BehaviorTerms ?? []))));
        AddListConstraint("Compatibility", NormalizeCompatibilityValues(args.Compatibility));
        AddSingleConstraint("Lifestyle", FormatLifestyleConstraint(args));
        AddListConstraint("Home", FormatHomeConstraints(args));

        return constraints;

        void AddListConstraint(string label, IEnumerable<string>? values)
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

        void AddSingleConstraint(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                constraints.Add(new AdoptionCopilotConstraint(label, value.Trim()));
            }
        }
    }

    private static IEnumerable<string> SplitSingle(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
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
                    "introduction",
                    "introductions",
                    "settle",
                    "settles",
                    "short walk",
                    "indoor",
                    "routine",
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
        if (!string.IsNullOrWhiteSpace(energyLevel) &&
            ContainsAny(energyLevel, ["low"]) &&
            args.Compatibility?.Any(value =>
                ContainsAny(value, ["senior", "older", "sick", "sensitive", "anxious"])) == true)
        {
            return "Calm";
        }

        return FormatLifestyleConstraint(energyLevel);
    }

    private static IReadOnlyList<string> FormatHomeConstraints(AdoptionCopilotSearchDogsArgs args)
    {
        var values = new List<string>();
        if (args.ApartmentFriendly == true ||
            ContainsAny(args.HomeType ?? string.Empty, ["apartment", "flat"]) ||
            ContainsAny(args.HousingPreference ?? string.Empty, ["apartment", "flat"]))
        {
            values.Add("Apartment");
        }

        if (args.YardFriendly == true ||
            args.YardRequired == true ||
            args.NeedsYard == true ||
            ContainsAny(args.HomeType ?? string.Empty, ["yard", "garden", "house"]) ||
            ContainsAny(args.HousingPreference ?? string.Empty, ["yard", "garden", "house"]))
        {
            values.Add("House with yard");
        }

        if (values.Count == 0 && !string.IsNullOrWhiteSpace(args.HomeType))
        {
            values.Add(args.HomeType.Trim());
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

    private static string NormalizeMatchLabel(string? label)
    {
        return label?.Trim() switch
        {
            "Excellent match" => "Excellent match",
            "Good match" => "Good match",
            "Possible match" => "Possible match",
            _ => "Good match"
        };
    }

    private static string GetMatchLabel(int scorePercent)
    {
        return scorePercent switch
        {
            >= 90 => "Excellent match",
            >= 74 => "Good match",
            _ => "Possible match"
        };
    }

    private static readonly string[] KnownClujNeighborhoods =
    [
        "Zorilor",
        "Manastur",
        "Mănăștur",
        "Marasti",
        "Mărăști",
        "Gheorgheni",
        "Grigorescu",
        "Buna Ziua",
        "Bună Ziua",
        "Iris",
        "Intre Lacuri",
        "Între Lacuri",
        "Andrei Muresanu",
        "Andrei Mureșanu",
        "Centru",
        "Borhanci",
        "Dambul Rotund",
        "Dâmbul Rotund",
        "Gruia",
        "Plopilor",
        "Someseni",
        "Someșeni"
    ];

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record DogDetailsToolArgs(int DogId);

    private sealed record CopilotToolExecutionResult(
        string OutputJson,
        AdoptionCopilotToolSearchResult? SearchResult,
        AdoptionCopilotToolDogCandidate? DogCandidate);

    private sealed record DetectedAgeConstraint(
        int? MaxAgeYears,
        int? MinAgeYears,
        string? Comparison);
}
