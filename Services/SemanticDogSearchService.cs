using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class SemanticDogSearchService(
    ApplicationDbContext context,
    IDogSearchEmbeddingService dogSearchEmbeddingService,
    IDogSearchDocumentService dogSearchDocumentService,
    IEmbeddingService embeddingService,
    IDogRecommendationService dogRecommendationService,
    IDistanceService distanceService,
    IOptions<OpenAiSettings> openAiOptions,
    ILogger<SemanticDogSearchService> logger) : ISemanticDogSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SemanticDogSearchResult>> SearchDogsAsync(
        string query,
        string? adopterUserId,
        int count = 10,
        SemanticDogSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var safeCount = Math.Max(1, count);
        var safeQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(safeQuery))
        {
            return [];
        }

        var recommendationMap = await BuildRecommendationMapAsync(adopterUserId, safeCount, cancellationToken);
        var settings = openAiOptions.Value;
        if (settings.Enabled && settings.HasApiKey)
        {
            var semanticResults = await TrySemanticSearchAsync(safeQuery, recommendationMap, safeCount, options, cancellationToken);
            if (semanticResults.Count > 0)
            {
                return semanticResults;
            }
        }

        return await KeywordFallbackSearchAsync(safeQuery, recommendationMap, safeCount, options, cancellationToken);
    }

    private async Task<Dictionary<int, DogRecommendationResult>> BuildRecommendationMapAsync(
        string? adopterUserId,
        int count,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adopterUserId))
        {
            return [];
        }

        try
        {
            var recommendations = await dogRecommendationService.GetRuleBasedRecommendationsAsync(adopterUserId, Math.Max(count, 25));
            return recommendations.ToDictionary(recommendation => recommendation.DogId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rule-based profile bonus could not be loaded for semantic dog search.");
            return [];
        }
    }

    private async Task<IReadOnlyList<SemanticDogSearchResult>> TrySemanticSearchAsync(
        string query,
        IReadOnlyDictionary<int, DogRecommendationResult> recommendationMap,
        int count,
        SemanticDogSearchOptions? options,
        CancellationToken cancellationToken)
    {
        try
        {
            var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
            if (queryEmbedding is null || queryEmbedding.Length == 0)
            {
                return [];
            }

            var embeddings = await dogSearchEmbeddingService.GetSearchableDogEmbeddingsAsync(cancellationToken);
            if (embeddings.Count == 0)
            {
                return [];
            }

            var results = new List<SemanticDogSearchResult>();
            foreach (var embeddingRow in embeddings)
            {
                if (embeddingRow.Dog is null || !DogMatchesOptions(embeddingRow.Dog, options, out var distanceKm))
                {
                    continue;
                }

                var embedding = DeserializeEmbedding(embeddingRow.EmbeddingJson);
                if (embedding is null)
                {
                    continue;
                }

                var similarity = embeddingService.CosineSimilarity(queryEmbedding, embedding);
                var recommendationBonus = recommendationMap.TryGetValue(embeddingRow.DogId, out var recommendation)
                    ? Math.Min(22, recommendation.MatchPercentage * 0.22)
                    : 0;
                var distanceBonus = distanceKm.HasValue ? Math.Max(0, 10 - (distanceKm.Value / 10)) : 0;
                var score = Math.Clamp((similarity * 72) + recommendationBonus + distanceBonus, 0, 96);

                results.Add(BuildResult(
                    embeddingRow.Dog,
                    (int)Math.Round(score),
                    true,
                    distanceKm,
                    recommendation,
                    similarity >= 0.25 ? "Profile details match your search." : "Closest available profile match."));
            }

            return results
                .OrderByDescending(result => result.ScorePercent)
                .ThenBy(result => result.Dog.Name)
                .Take(count)
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Semantic dog search failed. Falling back to keyword/rule-based search.");
            return [];
        }
    }

    private async Task<IReadOnlyList<SemanticDogSearchResult>> KeywordFallbackSearchAsync(
        string query,
        IReadOnlyDictionary<int, DogRecommendationResult> recommendationMap,
        int count,
        SemanticDogSearchOptions? options,
        CancellationToken cancellationToken)
    {
        var dogs = await context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.Images)
            .Include(d => d.PreferredFoodType)
            .Where(d => d.Status == DogStatus.Available || d.Status == DogStatus.Reserved)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var terms = Tokenize(query);
        var results = new List<SemanticDogSearchResult>();

        foreach (var dog in dogs)
        {
            if (!DogMatchesOptions(dog, options, out var distanceKm))
            {
                continue;
            }

            recommendationMap.TryGetValue(dog.Id, out var recommendation);
            var document = dogSearchDocumentService.BuildDocument(dog);
            var keywordScore = CalculateKeywordScore(document, terms);
            var recommendationBonus = recommendation?.MatchPercentage * 0.22 ?? 0;
            var distanceBonus = distanceKm.HasValue ? Math.Max(0, 10 - (distanceKm.Value / 10)) : 0;
            var total = Math.Clamp(45 + keywordScore + recommendationBonus + distanceBonus, 45, 92);

            if (keywordScore == 0 && recommendation is null && terms.Count > 0)
            {
                continue;
            }

            results.Add(BuildResult(
                dog,
                (int)Math.Round(total),
                false,
                distanceKm,
                recommendation,
                keywordScore > 0 ? "Matches the words in your search." : "Matches your adopter profile and preferences."));
        }

        if (results.Count == 0 && recommendationMap.Count > 0)
        {
            results.AddRange(recommendationMap.Values
                .Where(recommendation => DogMatchesOptions(recommendation.Dog, options, out _))
                .Take(count)
                .Select(recommendation => BuildResult(
                    recommendation.Dog,
                    recommendation.MatchPercentage,
                    false,
                    null,
                    recommendation,
                    "Closest rule-based match for your request.")));
        }

        return results
            .OrderByDescending(result => result.ScorePercent)
            .ThenBy(result => result.Dog.Name)
            .Take(count)
            .ToList();
    }

    private SemanticDogSearchResult BuildResult(
        Dog dog,
        int scorePercent,
        bool usedSemanticEmbeddings,
        double? distanceKm,
        DogRecommendationResult? recommendation,
        string primaryReason)
    {
        var reasons = new List<string> { primaryReason };
        if (distanceKm.HasValue)
        {
            reasons.Add($"{distanceKm.Value:0.#} km from the selected area.");
        }

        if (recommendation is not null)
        {
            reasons.AddRange(recommendation.Reasons);
        }

        reasons = reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return new SemanticDogSearchResult(
            dog.Id,
            dog,
            Math.Clamp(scorePercent, 45, 96),
            GetMatchLabel(scorePercent),
            reasons,
            BuildSearchSummary(dog, reasons[0]),
            distanceKm,
            usedSemanticEmbeddings);
    }

    private bool DogMatchesOptions(Dog dog, SemanticDogSearchOptions? options, out double? distanceKm)
    {
        distanceKm = null;
        if (dog.Status is not (DogStatus.Available or DogStatus.Reserved))
        {
            return false;
        }

        if (options is null)
        {
            return true;
        }

        if (options.Status.HasValue && dog.Status != options.Status.Value)
        {
            return false;
        }

        if (options.Size.HasValue && dog.Size != options.Size.Value)
        {
            return false;
        }

        if (options.ShelterId.HasValue && dog.ShelterId != options.ShelterId.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.Location) &&
            !string.Equals(dog.Location, options.Location.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.Neighborhood) &&
            !string.Equals(dog.Shelter?.Neighborhood, options.Neighborhood.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (options.OriginLatitude.HasValue && options.OriginLongitude.HasValue)
        {
            if (dog.Shelter?.Latitude is null || dog.Shelter.Longitude is null)
            {
                return false;
            }

            distanceKm = distanceService.CalculateDistanceKm(
                options.OriginLatitude.Value,
                options.OriginLongitude.Value,
                dog.Shelter!.Latitude!.Value,
                dog.Shelter.Longitude!.Value);

            if (options.RadiusKm.HasValue && distanceKm > options.RadiusKm.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static float[]? DeserializeEmbedding(string embeddingJson)
    {
        return JsonSerializer.Deserialize<float[]>(embeddingJson, JsonOptions);
    }

    private static List<string> Tokenize(string query)
    {
        return Regex.Matches(query.ToLowerInvariant(), "[a-z0-9]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 2 && term is not "dog" and not "dogs" and not "for" and not "near")
            .Distinct()
            .ToList();
    }

    private static double CalculateKeywordScore(string document, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return 0;
        }

        var lowerDocument = document.ToLowerInvariant();
        var matches = terms.Count(term => lowerDocument.Contains(term, StringComparison.OrdinalIgnoreCase));
        return (matches / (double)terms.Count) * 35;
    }

    private static string GetMatchLabel(int scorePercent)
    {
        return scorePercent switch
        {
            >= 84 => "Excellent match",
            >= 68 => "Good match",
            _ => "Possible match"
        };
    }

    private static string BuildSearchSummary(Dog dog, string reason)
    {
        return $"{dog.Name} could be worth a closer look because {reason.TrimEnd('.').ToLowerInvariant()}.";
    }
}
