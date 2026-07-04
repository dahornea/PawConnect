namespace PawConnect.Services;

public interface IAiReportSummaryService
{
    Task<AiReportSummaryResult> GenerateShelterSummaryAsync(
        ShelterReportSummaryMetricsDto metrics,
        CancellationToken cancellationToken = default);
}
