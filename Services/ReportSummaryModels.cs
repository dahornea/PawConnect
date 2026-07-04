namespace PawConnect.Services;

public sealed record LowStockResourceSummaryDto(
    string Name,
    string? Category,
    int Quantity,
    int LowStockThreshold,
    string Unit);

public sealed record ShelterReportSummaryMetricsDto(
    string ShelterName,
    string City,
    DateTime FromDate,
    DateTime ToDate,
    int TotalDogs,
    int AvailableDogs,
    int ReservedDogs,
    int AdoptedDogs,
    int InTreatmentDogs,
    int NewRequestsInPeriod,
    int PendingRequests,
    int ConfirmedVisitsInPeriod,
    int AcceptedRequests,
    int RejectedRequests,
    int CancelledRequests,
    int TotalRequests,
    int RecentlyAdoptedDogs,
    int LowStockResourceCount,
    IReadOnlyList<LowStockResourceSummaryDto> CriticalLowStockResources,
    double? AverageDecisionDays);

public sealed record AiReportSummaryRequest(
    string ReportType,
    ShelterReportSummaryMetricsDto ShelterMetrics);

public sealed record AiReportSummaryResult(
    string Title,
    string ExecutiveSummary,
    IReadOnlyList<string> KeyHighlights,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<string> Limitations,
    bool UsedAi,
    string? FallbackReason);

public sealed record OpenAiReportSummaryResponse(
    bool Success,
    AiReportSummaryResult? Summary,
    string? ErrorMessage)
{
    public static OpenAiReportSummaryResponse Failed(string reason)
    {
        return new OpenAiReportSummaryResponse(false, null, reason);
    }

    public static OpenAiReportSummaryResponse Successful(AiReportSummaryResult summary)
    {
        return new OpenAiReportSummaryResponse(true, summary, null);
    }
}
