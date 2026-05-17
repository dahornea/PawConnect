namespace PawConnect.Services;

public interface ISemanticDogSearchService
{
    Task<IReadOnlyList<SemanticDogSearchResult>> SearchDogsAsync(
        string query,
        string? adopterUserId,
        int count = 10,
        SemanticDogSearchOptions? options = null,
        CancellationToken cancellationToken = default);
}
