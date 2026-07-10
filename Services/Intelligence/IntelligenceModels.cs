using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed record IntelligenceContext(IntelligenceAudienceType AudienceType, string? UserId, int? ShelterId, DateTime UtcNow);

public sealed record IntelligenceScoreFactor(string Label, int Points, string Explanation);

public sealed record RecommendedActionDto(
    string Key,
    string Label,
    string Description,
    string ActionType,
    string Route,
    string RequiredRole,
    string? EntityType = null,
    string? EntityId = null,
    bool IsPrimary = false,
    bool RequiresConfirmation = false,
    bool IsAvailable = true,
    string? UnavailableReason = null);

public sealed record IntelligenceSignal(
    string Key,
    IntelligenceCategory Category,
    string SourceModule,
    string EntityType,
    string? EntityId,
    string? EntityDisplayName,
    string? OwnerUserId,
    int? ShelterId,
    string Title,
    string Summary,
    string WhyItMatters,
    string ResolutionCondition,
    string ThresholdDescription,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<IntelligenceScoreFactor> ScoreFactors,
    IReadOnlyList<RecommendedActionDto> RecommendedActions,
    DateTime ObservedAtUtc,
    string ConfidenceLabel = "High");

public sealed record IntelligenceEvaluationContext(IntelligenceContext Context, IReadOnlyCollection<IntelligenceSignal> Signals);

public sealed record OperationalInsightCandidate(
    string Fingerprint,
    IntelligenceAudienceType AudienceType,
    string? UserId,
    int? ShelterId,
    IntelligenceCategory Category,
    string InsightType,
    string SourceModule,
    string EntityType,
    string? EntityId,
    string? EntityDisplayName,
    string Title,
    string Summary,
    IntelligenceSeverity Severity,
    int PriorityScore,
    string ConfidenceLabel,
    string Explanation,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<IntelligenceScoreFactor> ScoreBreakdown,
    IReadOnlyList<RecommendedActionDto> RecommendedActions);

public sealed record IntelligenceEvaluationResult(
    IntelligenceAudienceType AudienceType,
    string? UserId,
    int? ShelterId,
    int ProvidersEvaluated,
    int ProviderFailures,
    int SignalsCollected,
    int Created,
    int Updated,
    int Resolved,
    DateTime EvaluatedAtUtc,
    TimeSpan Duration);

public sealed record IntelligenceInsightQuery(
    IntelligenceSeverity? Severity = null,
    IntelligenceCategory? Category = null,
    IntelligenceInsightStatus? Status = null,
    string? EntityType = null,
    string? Search = null,
    bool IncludeSnoozed = false,
    int Page = 1,
    int PageSize = 25);

public sealed record OperationalInsightDto(
    int Id,
    IntelligenceAudienceType AudienceType,
    string? UserId,
    int? ShelterId,
    IntelligenceCategory Category,
    string InsightType,
    string SourceModule,
    string EntityType,
    string? EntityId,
    string? EntityDisplayName,
    string Title,
    string Summary,
    IntelligenceSeverity Severity,
    int PriorityScore,
    string ConfidenceLabel,
    string Explanation,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<IntelligenceScoreFactor> ScoreBreakdown,
    IReadOnlyList<RecommendedActionDto> RecommendedActions,
    IntelligenceInsightStatus Status,
    DateTime FirstDetectedAtUtc,
    DateTime LastDetectedAtUtc,
    DateTime LastEvaluatedAtUtc,
    DateTime? AcknowledgedAtUtc,
    DateTime? SnoozedUntilUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionReason);

public sealed record OperationalInsightListItemDto(
    int Id,
    IntelligenceCategory Category,
    string EntityType,
    string? EntityDisplayName,
    string Title,
    string Summary,
    IntelligenceSeverity Severity,
    int PriorityScore,
    IReadOnlyList<string> EvidencePreview,
    int EvidenceCount,
    IReadOnlyList<RecommendedActionDto> RecommendedActions,
    IntelligenceInsightStatus Status,
    DateTime FirstDetectedAtUtc);

public sealed record IntelligenceSummaryDto(
    int Total,
    int Critical,
    int High,
    int Medium,
    int Acknowledged,
    int Snoozed,
    int Resolved,
    DateTime? LastRefreshedAtUtc,
    string WorkloadLabel,
    IReadOnlyDictionary<IntelligenceCategory, int> ByCategory);

public sealed record IntelligenceScope(IntelligenceAudienceType AudienceType, string? UserId = null, int? ShelterId = null);

