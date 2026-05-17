namespace PawConnect.Services;

public interface IAdoptionCopilotToolService
{
    Task<AdoptionCopilotToolSearchResult> SearchDogsAsync(
        string adopterUserId,
        AdoptionCopilotSearchDogsArgs args,
        CancellationToken cancellationToken = default);

    Task<AdoptionCopilotProfileToolResult?> GetAdopterProfileSummaryAsync(
        string adopterUserId,
        CancellationToken cancellationToken = default);

    Task<AdoptionCopilotPreferenceToolResult> GetFavoriteAndRecentPreferencesAsync(
        string adopterUserId,
        CancellationToken cancellationToken = default);

    Task<AdoptionCopilotToolDogCandidate?> GetDogDetailsPublicAsync(
        int dogId,
        CancellationToken cancellationToken = default);
}
