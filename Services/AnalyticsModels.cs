using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record AnalyticsDateRange(DateTime StartUtc, DateTime EndUtc, string Label)
{
    public TimeSpan Duration => EndUtc - StartUtc;

    public int TotalDays => Math.Max(1, (int)Math.Ceiling(Duration.TotalDays));

    public void Validate()
    {
        if (StartUtc >= EndUtc)
        {
            throw new ArgumentException("Analytics start date must be before the end date.");
        }

        if (Duration.TotalDays > 366)
        {
            throw new ArgumentException("Analytics date ranges are limited to one year.");
        }
    }

    public static AnalyticsDateRange LastDays(int days, DateTime? utcNow = null)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "The number of days must be greater than zero.");
        }

        var todayUtc = (utcNow ?? DateTime.UtcNow).Date;
        var startUtc = todayUtc.AddDays(-(days - 1));
        return new AnalyticsDateRange(startUtc, todayUtc.AddDays(1), $"Last {days} days");
    }
}

public sealed record AnalyticsShelterOptionDto(int Id, string Name);

public sealed record AnalyticsSummaryCardDto(string Label, string Value, string HelperText, string Tone = "default");

public sealed record AdoptionFunnelDto(
    int SubmittedRequests,
    int PendingRequests,
    int VisitConfirmedRequests,
    int AcceptedRequests,
    int RejectedRequests,
    int CancelledRequests,
    double ConversionRate,
    double VisitConfirmationRate,
    double RejectionCancellationRate,
    double? AverageHoursToVisitConfirmation,
    double? AverageHoursToFinalDecision,
    double? AveragePendingAgeHours);

public sealed record AdoptionTrendPointDto(
    DateTime PeriodStart,
    string Label,
    int SubmittedRequests,
    int VisitConfirmedRequests,
    int AcceptedRequests,
    int RejectedOrCancelledRequests);

public sealed record StatusBreakdownDto(string Label, int Count, double Percent);

public sealed record ShelterWorkloadDto(
    int ShelterId,
    string ShelterName,
    int DogsManaged,
    int RequestsInRange,
    int PendingRequests,
    int AcceptedRequests,
    int LowStockResources,
    double? AveragePendingAgeHours,
    double? AverageFinalDecisionHours);

public sealed record DogVisibilityDto(
    int DogId,
    string DogName,
    string ShelterName,
    DogStatus Status,
    int RecentViewActivity,
    int Favorites,
    int AdoptionRequests,
    string Insight);

public sealed record ResourceCategoryAnalyticsDto(string CategoryName, int LowStockCount, int TotalResources);

public sealed record LowStockResourceDto(
    int ResourceId,
    string ResourceName,
    string ShelterName,
    string CategoryName,
    int Quantity,
    int LowStockThreshold,
    string Unit);

public sealed record ResourceAnalyticsDto(
    int TotalResources,
    int LowStockResources,
    IReadOnlyList<ResourceCategoryAnalyticsDto> LowStockByCategory,
    IReadOnlyList<LowStockResourceDto> ResourcesClosestToThreshold);

public sealed record ReportTypeAnalyticsDto(string ReportType, int Count, int SuccessfulCount, int FailedCount);

public sealed record ReportActivityAnalyticsDto(
    int ReportsGenerated,
    int ReportsSent,
    int FailedReports,
    IReadOnlyList<ReportTypeAnalyticsDto> ReportsByType);

public sealed record CopilotIntentAnalyticsDto(string Intent, int Count);

public sealed record CopilotAnalyticsDto(
    int Sessions,
    int AiEnhancedSessions,
    int FallbackSessions,
    int SemanticSearchSessions,
    int ToolCallingSessions,
    double AverageResultCount,
    int PositiveFeedback,
    int NegativeFeedback,
    IReadOnlyList<CopilotIntentAnalyticsDto> TopIntents);

public sealed record AdminAnalyticsDashboardDto(
    AnalyticsDateRange Range,
    int? ShelterFilterId,
    IReadOnlyList<AnalyticsShelterOptionDto> ShelterOptions,
    IReadOnlyList<AnalyticsSummaryCardDto> SummaryCards,
    AdoptionFunnelDto AdoptionFunnel,
    IReadOnlyList<AdoptionTrendPointDto> RequestTrends,
    IReadOnlyList<StatusBreakdownDto> DogStatusBreakdown,
    IReadOnlyList<ShelterWorkloadDto> ShelterWorkload,
    IReadOnlyList<DogVisibilityDto> MostViewedDogs,
    IReadOnlyList<DogVisibilityDto> MostFavoritedDogs,
    IReadOnlyList<DogVisibilityDto> LowEngagementDogs,
    ResourceAnalyticsDto ResourceAnalytics,
    ReportActivityAnalyticsDto ReportActivity,
    CopilotAnalyticsDto? CopilotAnalytics);

public sealed record ShelterAnalyticsDashboardDto(
    AnalyticsDateRange Range,
    string ShelterName,
    IReadOnlyList<AnalyticsSummaryCardDto> SummaryCards,
    AdoptionFunnelDto AdoptionFunnel,
    IReadOnlyList<AdoptionTrendPointDto> RequestTrends,
    IReadOnlyList<StatusBreakdownDto> DogStatusBreakdown,
    ShelterWorkloadDto Workload,
    IReadOnlyList<DogVisibilityDto> MostViewedDogs,
    IReadOnlyList<DogVisibilityDto> MostFavoritedDogs,
    IReadOnlyList<DogVisibilityDto> LowEngagementDogs,
    ResourceAnalyticsDto ResourceAnalytics,
    ReportActivityAnalyticsDto ReportActivity);
