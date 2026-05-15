namespace PawConnect.Services;

public sealed record RecommendationOpenAiRequest(
    RecommendationAdopterProfileInput AdopterProfile,
    IReadOnlyList<RecommendationDogCandidateInput> Candidates);

public sealed record RecommendationAdopterProfileInput(
    string? City,
    string HousingType,
    bool HasYard,
    bool HasOtherPets,
    bool HasChildren,
    string? ExperienceWithDogs);

public sealed record RecommendationDogCandidateInput(
    int DogId,
    string Breed,
    string Age,
    string Size,
    string Status,
    int MatchPercentage,
    string MatchLabel,
    string? RuleBasedSummary,
    string? PublicDescription,
    string? BehaviorDescription,
    string? ShelterCity,
    double? DistanceKm,
    IReadOnlyList<string> RuleBasedReasons,
    IReadOnlyList<RecommendationReasonInput> RuleBasedReasonCategories);

public sealed record RecommendationReasonInput(
    string Category,
    string Text);

public sealed record OpenAiRecommendationResponse(
    bool Success,
    IReadOnlyList<OpenAiRecommendationItem> Recommendations,
    string? ErrorMessage = null)
{
    public static OpenAiRecommendationResponse Failed(string? errorMessage = null)
    {
        return new OpenAiRecommendationResponse(false, [], errorMessage);
    }
}

public sealed record OpenAiRecommendationItem(
    int DogId,
    int Rank,
    string MatchLabel,
    IReadOnlyList<string> Reasons,
    string? ShortSummary = null,
    IReadOnlyList<OpenAiRecommendationReason>? Categories = null);

public sealed record OpenAiRecommendationReason(
    string Category,
    string Text);
