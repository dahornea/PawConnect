namespace PawConnect.Services;

public interface IShelterSummaryReportService
{
    Task SendShelterSummaryReportAsync(int shelterId, CancellationToken cancellationToken = default);

    Task<int> SendScheduledShelterSummaryReportsAsync(CancellationToken cancellationToken = default);
}
