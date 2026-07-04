namespace PawConnect.Services;

public interface IOpenAiNaturalLanguageSearchClient
{
    Task<OpenAiNaturalLanguageSearchResponse> InterpretAsync(
        NaturalLanguageSearchAiRequest request,
        CancellationToken cancellationToken = default);
}
