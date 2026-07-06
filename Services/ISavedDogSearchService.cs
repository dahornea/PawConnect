using PawConnect.Entities;

namespace PawConnect.Services;

public interface ISavedDogSearchService
{
    Task<IReadOnlyList<SavedDogSearchDto>> GetSavedSearchesForAdopterAsync(string adopterUserId, CancellationToken cancellationToken = default);

    Task<SavedDogSearchDetailsDto?> GetSavedSearchDetailsAsync(int savedSearchId, string adopterUserId, CancellationToken cancellationToken = default);

    Task<SavedSearchStatsDto> GetStatsForAdopterAsync(string adopterUserId, CancellationToken cancellationToken = default);

    Task<SavedDogSearchDto> CreateSavedSearchAsync(string adopterUserId, SavedDogSearchCreateRequest request, CancellationToken cancellationToken = default);

    Task<SavedDogSearchDto> UpdateSavedSearchAsync(int savedSearchId, string adopterUserId, SavedDogSearchUpdateRequest request, CancellationToken cancellationToken = default);

    Task DeleteSavedSearchAsync(int savedSearchId, string adopterUserId, CancellationToken cancellationToken = default);

    Task<SavedDogSearchDetailsDto?> EvaluateSavedSearchAsync(int savedSearchId, string adopterUserId, CancellationToken cancellationToken = default);

    Task EvaluateDogAgainstActiveSavedSearchesAsync(int dogId, CancellationToken cancellationToken = default);

    Task SetAlertsAsync(int savedSearchId, string adopterUserId, bool enabled, CancellationToken cancellationToken = default);

    Task MarkMatchAsSeenAsync(int matchId, string adopterUserId, CancellationToken cancellationToken = default);

    Task DismissMatchAsync(int matchId, string adopterUserId, CancellationToken cancellationToken = default);
}
