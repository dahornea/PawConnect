using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record CopilotHistoryItemDto(
    int Id,
    DateTime CreatedAt,
    string QuerySummary,
    int ResultCount,
    bool UsedAiEnhancement,
    bool UsedSemanticSearch,
    bool UsedToolCalling,
    string? FallbackReason,
    IReadOnlyList<AdoptionCopilotConstraint> AppliedConstraints);

public sealed record CopilotSessionDto(
    int Id,
    DateTime CreatedAt,
    string QuerySummary,
    IReadOnlyList<int> ResultDogIds,
    int ResultCount,
    bool UsedAiEnhancement,
    bool UsedSemanticSearch,
    bool UsedToolCalling,
    string? FallbackReason,
    IReadOnlyList<AdoptionCopilotConstraint> AppliedConstraints);

public sealed record SubmitCopilotFeedbackRequest(
    int SessionId,
    int DogId,
    CopilotFeedbackType FeedbackType,
    string? OptionalComment = null,
    bool WasOpened = false,
    bool WasFavorited = false);

public sealed record CopilotFeedbackDto(
    int Id,
    int CopilotSessionId,
    int DogId,
    CopilotFeedbackType FeedbackType,
    DateTime CreatedAt,
    string? OptionalComment,
    bool WasOpened,
    bool WasFavorited);

public sealed record CopilotExplanationDto(
    int SessionId,
    int DogId,
    string DogName,
    string UserQuerySummary,
    IReadOnlyList<AdoptionCopilotConstraint> MatchedCriteria,
    IReadOnlyList<string> DirectEvidence,
    IReadOnlyList<string> IndirectEvidence,
    IReadOnlyList<string> CautionEvidence,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> SuggestedQuestions,
    string AdvisoryDisclaimer);
