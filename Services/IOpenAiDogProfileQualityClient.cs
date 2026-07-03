namespace PawConnect.Services;

public interface IOpenAiDogProfileQualityClient
{
    Task<OpenAiDogProfileQualityResponse> CheckAsync(
        DogProfileQualityRequest request,
        DogProfileQualityResult deterministicResult,
        CancellationToken cancellationToken = default);
}
