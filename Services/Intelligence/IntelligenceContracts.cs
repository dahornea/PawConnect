namespace PawConnect.Services.Intelligence;

public interface IIntelligenceSignalProvider
{
    string ProviderKey { get; }
    Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(IntelligenceContext context, CancellationToken cancellationToken);
}

public interface IIntelligenceRule
{
    string RuleKey { get; }
    Task<IReadOnlyCollection<OperationalInsightCandidate>> EvaluateAsync(IntelligenceEvaluationContext context, CancellationToken cancellationToken);
}

public interface IIntelligenceEngine
{
    Task<IntelligenceEvaluationResult> EvaluateForShelterAsync(int shelterId, CancellationToken cancellationToken = default);
    Task<IntelligenceEvaluationResult> EvaluateForAdminAsync(CancellationToken cancellationToken = default);
    Task<IntelligenceEvaluationResult> EvaluateForAdopterAsync(string userId, CancellationToken cancellationToken = default);
    Task RefreshActiveInsightsAsync(CancellationToken cancellationToken = default);
}

public interface IIntelligenceInsightService
{
    Task<PagedResult<OperationalInsightListItemDto>> GetInsightSummariesAsync(IntelligenceScope scope, IntelligenceInsightQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<OperationalInsightDto>> GetInsightsAsync(IntelligenceScope scope, IntelligenceInsightQuery query, CancellationToken cancellationToken = default);
    Task<OperationalInsightDto?> GetInsightDetailsAsync(int insightId, IntelligenceScope scope, CancellationToken cancellationToken = default);
    Task<IntelligenceSummaryDto> GetSummaryAsync(IntelligenceScope scope, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(int insightId, IntelligenceScope scope, string actorUserId, CancellationToken cancellationToken = default);
    Task SnoozeAsync(int insightId, IntelligenceScope scope, string actorUserId, TimeSpan duration, CancellationToken cancellationToken = default);
    Task ResolveAsync(int insightId, IntelligenceScope scope, string actorUserId, string reason, CancellationToken cancellationToken = default);
    Task ReopenAsync(int insightId, IntelligenceScope scope, string actorUserId, CancellationToken cancellationToken = default);
    Task<IntelligenceEvaluationResult> RefreshAsync(IntelligenceScope scope, string actorUserId, CancellationToken cancellationToken = default);
}

public interface IIntelligenceRecommendationService
{
    Task<IReadOnlyList<RecommendedActionDto>> ValidateActionsAsync(IReadOnlyList<RecommendedActionDto> actions, IntelligenceScope scope, CancellationToken cancellationToken = default);
}
