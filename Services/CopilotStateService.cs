namespace PawConnect.Services;

public interface ICopilotStateService
{
    CopilotSessionState? GetState(string? userId);

    void SaveState(string userId, string query, AdoptionCopilotResponse response);

    void ClearState(string? userId);
}

public sealed class CopilotStateService : ICopilotStateService
{
    private CopilotSessionState? _state;

    public CopilotSessionState? GetState(string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId) &&
            string.Equals(_state?.UserId, userId, StringComparison.Ordinal)
            ? _state
            : null;
    }

    public void SaveState(string userId, string query, AdoptionCopilotResponse response)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        _state = new CopilotSessionState(
            userId,
            query.Trim(),
            response.AssistantMessage,
            response.AppliedConstraints?.ToList() ?? [],
            response.Results.Select(result => new CopilotSavedDogResult(
                result.DogId,
                result.ScorePercent,
                result.MatchLabel,
                result.Reasons.ToList(),
                result.SuggestedNextAction,
                result.DistanceKm,
                result.UsedAiEnhancement,
                result.MatchedCriteria?.ToList() ?? [],
                result.DisplayTags?.ToList() ?? [],
                result.CautionTags?.ToList() ?? [])).ToList(),
            response.UsedAiEnhancement,
            response.UsedSemanticSearch,
            response.UsedToolCalling,
            response.FallbackReason,
            DateTime.UtcNow);
    }

    public void ClearState(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.Equals(_state?.UserId, userId, StringComparison.Ordinal))
        {
            _state = null;
        }
    }
}

public sealed record CopilotSessionState(
    string UserId,
    string LastQuery,
    string LastAssistantMessage,
    IReadOnlyList<AdoptionCopilotConstraint> LastAppliedConstraints,
    IReadOnlyList<CopilotSavedDogResult> LastResults,
    bool LastUsedOpenAi,
    bool LastUsedSemanticSearch,
    bool LastUsedToolCalling,
    string? LastFallbackReason,
    DateTime LastGeneratedAt);

public sealed record CopilotSavedDogResult(
    int DogId,
    int ScorePercent,
    string MatchLabel,
    IReadOnlyList<string> Reasons,
    string SuggestedNextAction,
    double? DistanceKm,
    bool UsedAiEnhancement,
    IReadOnlyList<AdoptionCopilotConstraint> MatchedCriteria,
    IReadOnlyList<string> DisplayTags,
    IReadOnlyList<string> CautionTags);
