namespace PawConnect.Services;

public interface IOpenAiRecommendationClient
{
    Task<OpenAiRecommendationResponse> GetEnhancedRecommendationsAsync(
        RecommendationOpenAiRequest request,
        CancellationToken cancellationToken = default);
}
