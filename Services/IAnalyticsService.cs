namespace PawConnect.Services;

public interface IAnalyticsService
{
    Task<IReadOnlyList<AnalyticsShelterOptionDto>> GetAdminShelterOptionsAsync(
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<AdminAnalyticsDashboardDto> GetAdminAnalyticsAsync(
        AnalyticsDateRange range,
        int? shelterId,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<ShelterAnalyticsDashboardDto> GetShelterAnalyticsAsync(
        AnalyticsDateRange range,
        string shelterUserId,
        CancellationToken cancellationToken = default);
}
