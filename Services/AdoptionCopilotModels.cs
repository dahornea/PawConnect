using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record AdoptionCopilotResponse(
    string AssistantMessage,
    IReadOnlyList<AdoptionCopilotDogResult> Results,
    bool UsedAiEnhancement,
    bool UsedSemanticSearch,
    bool UsedToolCalling = false,
    string? FallbackReason = null,
    IReadOnlyList<AdoptionCopilotConstraint>? AppliedConstraints = null);

public sealed record AdoptionCopilotConstraint(
    string Label,
    string Value);

public sealed record AdoptionCopilotDogResult(
    int DogId,
    Dog Dog,
    int ScorePercent,
    string MatchLabel,
    IReadOnlyList<string> Reasons,
    string SuggestedNextAction,
    double? DistanceKm = null,
    bool UsedAiEnhancement = false,
    IReadOnlyList<AdoptionCopilotConstraint>? MatchedCriteria = null,
    IReadOnlyList<string>? DisplayTags = null,
    IReadOnlyList<string>? CautionTags = null);

public sealed record AdoptionCopilotToolOpenAiRequest(
    string UserMessage,
    IReadOnlyList<AdoptionCopilotConstraint> DeterministicConstraints);

public sealed record OpenAiAdoptionCopilotResponse(
    bool Success,
    string? AssistantMessage,
    IReadOnlyList<OpenAiAdoptionCopilotItem> Results,
    string? ErrorMessage = null)
{
    public static OpenAiAdoptionCopilotResponse Failed(string? errorMessage = null)
    {
        return new OpenAiAdoptionCopilotResponse(false, null, [], errorMessage);
    }
}

public sealed record OpenAiAdoptionCopilotItem(
    int DogId,
    int Rank,
    string MatchLabel,
    int ScorePercent,
    IReadOnlyList<string> Reasons,
    string SuggestedNextAction,
    IReadOnlyList<string>? DisplayTags = null,
    IReadOnlyList<string>? CautionTags = null,
    string? ShortSelectionRationale = null);

public sealed record OpenAiCopilotToolCall(
    string CallId,
    string Name,
    string ArgumentsJson);

public sealed record OpenAiCopilotToolOutput(
    string CallId,
    string Name,
    string OutputJson);

public delegate Task<OpenAiCopilotToolOutput> OpenAiCopilotToolExecutor(
    OpenAiCopilotToolCall toolCall,
    CancellationToken cancellationToken);
