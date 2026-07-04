namespace PawConnect.Services;

public interface INaturalLanguageSearchService
{
    Task<NaturalLanguageSearchResult> SearchAdminAsync(
        NaturalLanguageSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<NaturalLanguageSearchResult> SearchShelterAsync(
        NaturalLanguageSearchRequest request,
        CancellationToken cancellationToken = default);
}
