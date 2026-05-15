using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record DogRecommendationReason(
    string Category,
    string Text,
    int Weight);

public sealed record DogRecommendationResult(
    int DogId,
    Dog Dog,
    double Score,
    string MatchLevel,
    IReadOnlyList<string> Reasons,
    double? DistanceKm = null,
    bool UsedAiEnhancement = false,
    int MatchPercentage = 0,
    string? ShortSummary = null,
    IReadOnlyList<DogRecommendationReason>? ReasonCategories = null);
