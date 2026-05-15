namespace PawConnect.Services;

public interface IDogRecommendationService
{
    Task<IReadOnlyList<DogRecommendationResult>> GetRecommendationsForAdopterAsync(string adopterUserId, int count = 10);

    Task<IReadOnlyList<DogRecommendationResult>> GetRuleBasedRecommendationsAsync(string adopterUserId, int count = 10);

    Task<IReadOnlyList<DogRecommendationResult>> GetOpenAiEnhancedRecommendationsAsync(
        string adopterUserId,
        IReadOnlyList<DogRecommendationResult> candidates,
        int count = 10);
}
