namespace PawConnect.Services;

public sealed record DogProfileCompletenessDto(
    int DogId,
    string DogName,
    int ScorePercent,
    string Label,
    int CompletedItems,
    int TotalItems,
    IReadOnlyList<DogProfileCompletenessSectionDto> Sections,
    IReadOnlyList<DogProfileCompletenessMissingItemDto> MissingItems,
    IReadOnlyList<DogProfileCompletenessRecommendationDto> Recommendations,
    IReadOnlyList<string> AttentionFlags,
    DateTime LastCalculatedAtUtc);

public sealed record DogProfileCompletenessSectionDto(
    string Name,
    int ScorePercent,
    int CompletedItems,
    int TotalItems,
    int WeightPercent,
    IReadOnlyList<DogProfileCompletenessMissingItemDto> MissingItems);

public sealed record DogProfileCompletenessMissingItemDto(
    string Section,
    string Label,
    string Description,
    string? FieldName = null,
    bool IsCritical = false);

public sealed record DogProfileCompletenessRecommendationDto(
    string Title,
    string Description,
    string? FieldName = null);

public sealed record DogProfileCompletenessSummaryDto(
    int TotalDogs,
    double AverageScorePercent,
    int ExcellentCount,
    int GoodCount,
    int NeedsWorkCount,
    int IncompleteCount,
    IReadOnlyList<DogProfileCompletenessDto> DogsNeedingAttention);
