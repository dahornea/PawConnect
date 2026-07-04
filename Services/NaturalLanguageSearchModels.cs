using PawConnect.Entities;

namespace PawConnect.Services;

public enum NaturalLanguageSearchScope
{
    Unknown,
    AdoptionRequests,
    Dogs,
    Shelters,
    Resources,
    Reports,
    ActivityLogs,
    PlatformOverview
}

public enum NaturalLanguageSearchIntent
{
    Unknown,
    FindPendingRequests,
    FindRequestsByStatus,
    FindRequestsByDateRange,
    FindRequestsWaitingForVisitConfirmation,
    FindDogsByStatus,
    FindReservedDogsTooLong,
    FindDogsWithLowVisibility,
    FindDogsWithNoRequests,
    FindLowStockResources,
    FindSheltersWithLowStock,
    FindSheltersWithManyPendingRequests,
    FindPendingShelterApplications,
    FindReportsByDateRange,
    FindUpcomingVisits,
    FindRecentActivity
}

public enum NaturalLanguageSearchSortDirection
{
    Descending,
    Ascending
}

public sealed record NaturalLanguageSearchDateRange(
    DateTime? From,
    DateTime? To,
    string? Label);

public sealed class NaturalLanguageSearchInterpretation
{
    public NaturalLanguageSearchIntent Intent { get; set; } = NaturalLanguageSearchIntent.Unknown;

    public NaturalLanguageSearchScope Scope { get; set; } = NaturalLanguageSearchScope.Unknown;

    public double Confidence { get; set; }

    public AdoptionRequestStatus? RequestStatus { get; set; }

    public AdoptionVisitStatus? VisitStatus { get; set; }

    public DogStatus? DogStatus { get; set; }

    public ShelterRegistrationRequestStatus? ShelterApplicationStatus { get; set; }

    public string? DogName { get; set; }

    public string? ShelterName { get; set; }

    public string? City { get; set; }

    public string? ResourceCategory { get; set; }

    public bool LowStockOnly { get; set; }

    public bool NoRequestsOnly { get; set; }

    public int? OlderThanDays { get; set; }

    public NaturalLanguageSearchDateRange? DateRange { get; set; }

    public string? SortField { get; set; }

    public NaturalLanguageSearchSortDirection SortDirection { get; set; } = NaturalLanguageSearchSortDirection.Descending;

    public int Limit { get; set; } = 50;

    public bool NeedsClarification { get; set; }

    public string? ClarificationQuestion { get; set; }

    public string Explanation { get; set; } = "PawConnect could not interpret the query.";

    public bool UsedAi { get; set; }

    public string? FallbackReason { get; set; }

    public List<string> Warnings { get; set; } = [];
}

public sealed record NaturalLanguageSearchRequest(
    string Query,
    string CurrentUserId);

public sealed record NaturalLanguageSearchResult(
    string Query,
    NaturalLanguageSearchInterpretation Interpretation,
    IReadOnlyList<NaturalLanguageSearchResultItem> Items,
    string Message);

public sealed record NaturalLanguageSearchResultItem(
    string Id,
    string Type,
    string Title,
    string? Subtitle,
    string? Status,
    string? Description,
    DateTime? CreatedAt,
    DateTime? RelatedDate,
    string? Link,
    IReadOnlyList<string> Chips,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record NaturalLanguageSearchAiRequest(
    string Query,
    string Role,
    DateTime CurrentUtc,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedIntents,
    IReadOnlyList<string> AllowedRequestStatuses,
    IReadOnlyList<string> AllowedVisitStatuses,
    IReadOnlyList<string> AllowedDogStatuses,
    IReadOnlyList<string> AllowedSortFields);

public sealed record OpenAiNaturalLanguageSearchResponse(
    bool Success,
    NaturalLanguageSearchInterpretation? Interpretation,
    string? ErrorMessage)
{
    public static OpenAiNaturalLanguageSearchResponse Failed(string reason)
    {
        return new OpenAiNaturalLanguageSearchResponse(false, null, reason);
    }

    public static OpenAiNaturalLanguageSearchResponse Successful(NaturalLanguageSearchInterpretation interpretation)
    {
        return new OpenAiNaturalLanguageSearchResponse(true, interpretation, null);
    }
}
