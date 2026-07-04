using PawConnect.Entities;

namespace PawConnect.Services;

public enum ShelterOperationsBriefPeriod
{
    Today = 0,
    Next7Days = 1,
    Last30Days = 2
}

public enum ShelterOperationsPriority
{
    Critical = 0,
    High = 1,
    Medium = 2,
    Low = 3,
    Info = 4
}

public enum ShelterOperationsCategory
{
    AdoptionRequest = 0,
    Visit = 1,
    DogStatus = 2,
    DogProfile = 3,
    ResourceStock = 4,
    Notification = 5,
    Report = 6,
    SystemInfo = 7
}

public sealed record ShelterOperationsBriefRequest(ShelterOperationsBriefPeriod Period = ShelterOperationsBriefPeriod.Today);

public sealed record ShelterOperationsMetricDto(string Label, string Value, string HelpText, ShelterOperationsPriority Priority);

public sealed record ShelterPriorityItemDto(
    ShelterOperationsPriority Priority,
    ShelterOperationsCategory Category,
    string Title,
    string Description,
    string? RelatedEntityType,
    string? RelatedEntityId,
    string? ActionLink,
    string? ActionLabel,
    bool IsAiGeneratedText = false);

public sealed record ShelterSuggestedActionDto(
    string Title,
    string Description,
    string? ActionLink,
    string? ActionLabel,
    ShelterOperationsPriority Priority);

public sealed record ShelterAssistantInsightDto(
    string Title,
    string Description,
    ShelterOperationsCategory Category,
    ShelterOperationsPriority Priority,
    string? Link = null);

public sealed record ShelterAssistantDraftMessageDto(
    int AdoptionRequestId,
    string DogName,
    string Purpose,
    string DraftBody,
    bool UsedAi,
    string? FallbackReason);

public sealed record ShelterOperationsVisitDto(
    int AdoptionRequestId,
    string DogName,
    DateTime? PreferredVisitDateTime,
    string VisitStatus,
    string ActionLink);

public sealed record ShelterOperationsRequestHighlightDto(
    int AdoptionRequestId,
    string DogName,
    string Status,
    string VisitStatus,
    DateTime CreatedAt,
    int AgeDays,
    string ActionLink);

public sealed record ShelterOperationsResourceItemDto(
    int ResourceId,
    string Name,
    string Category,
    int Quantity,
    int LowStockThreshold,
    string Unit,
    ShelterOperationsPriority Priority,
    string ActionLink);

public sealed record ShelterOperationsDogProfileItemDto(
    int DogId,
    string DogName,
    DogStatus Status,
    IReadOnlyList<string> MissingFields,
    string ActionLink);

public sealed record ShelterOperationsBriefDto(
    DateTime GeneratedAt,
    ShelterOperationsBriefPeriod Period,
    int ShelterId,
    string ShelterName,
    bool UsedAi,
    string? FallbackReason,
    string ExecutiveSummary,
    IReadOnlyList<ShelterOperationsMetricDto> Metrics,
    IReadOnlyList<ShelterPriorityItemDto> PriorityItems,
    IReadOnlyList<ShelterSuggestedActionDto> SuggestedActions,
    IReadOnlyList<ShelterOperationsVisitDto> UpcomingVisits,
    IReadOnlyList<ShelterOperationsResourceItemDto> LowStockItems,
    IReadOnlyList<ShelterOperationsRequestHighlightDto> RequestHighlights,
    IReadOnlyList<ShelterOperationsDogProfileItemDto> DogProfileHighlights,
    IReadOnlyList<ShelterAssistantInsightDto> Insights,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, string> Links);

public sealed record ShelterOperationsBriefInputDto(
    DateTime GeneratedAt,
    ShelterOperationsBriefPeriod Period,
    string ShelterName,
    IReadOnlyList<ShelterOperationsMetricDto> Metrics,
    IReadOnlyList<ShelterPriorityItemDto> PriorityItems,
    IReadOnlyList<ShelterSuggestedActionDto> SuggestedActions,
    IReadOnlyList<ShelterOperationsVisitDto> UpcomingVisits,
    IReadOnlyList<ShelterOperationsResourceItemDto> LowStockItems,
    IReadOnlyList<ShelterOperationsRequestHighlightDto> RequestHighlights,
    IReadOnlyList<ShelterOperationsDogProfileItemDto> DogProfileHighlights,
    IReadOnlyList<ShelterAssistantInsightDto> Insights);

public sealed record ShelterOperationsAiPriorityItemDto(
    string Priority,
    string Category,
    string Title,
    string Description,
    string SuggestedAction);

public sealed record ShelterOperationsAiBriefDto(
    string ExecutiveSummary,
    IReadOnlyList<ShelterOperationsAiPriorityItemDto> PriorityItems,
    IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Limitations);

public sealed record OpenAiShelterOperationsAssistantResponse(
    bool Success,
    ShelterOperationsAiBriefDto? Brief,
    string? ErrorMessage)
{
    public static OpenAiShelterOperationsAssistantResponse Failed(string reason)
    {
        return new OpenAiShelterOperationsAssistantResponse(false, null, reason);
    }

    public static OpenAiShelterOperationsAssistantResponse Successful(ShelterOperationsAiBriefDto brief)
    {
        return new OpenAiShelterOperationsAssistantResponse(true, brief, null);
    }
}
