using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class StandardIntelligenceRule : IIntelligenceRule
{
    public string RuleKey => "StandardOperationalSignal";

    public Task<IReadOnlyCollection<OperationalInsightCandidate>> EvaluateAsync(
        IntelligenceEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var candidates = context.Signals.Select(signal =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var score = Math.Clamp(signal.ScoreFactors.Sum(factor => factor.Points), 0, 100);
            var severity = score switch
            {
                >= 85 => IntelligenceSeverity.Critical,
                >= 70 => IntelligenceSeverity.High,
                >= 50 => IntelligenceSeverity.Medium,
                >= 25 => IntelligenceSeverity.Low,
                _ => IntelligenceSeverity.Informational
            };

            var scopeKey = context.Context.AudienceType switch
            {
                IntelligenceAudienceType.Shelter => $"shelter:{context.Context.ShelterId}",
                IntelligenceAudienceType.Adopter => $"user:{context.Context.UserId}",
                _ => "platform"
            };
            var explanation = $"What happened: {signal.Summary} Why it matters: {signal.WhyItMatters} " +
                              $"Trigger: {signal.ThresholdDescription} This insight resolves when {signal.ResolutionCondition}";

            return new OperationalInsightCandidate(
                $"{context.Context.AudienceType}:{scopeKey}:{signal.Key}",
                context.Context.AudienceType,
                context.Context.AudienceType == IntelligenceAudienceType.Adopter ? context.Context.UserId : null,
                context.Context.AudienceType == IntelligenceAudienceType.Shelter ? context.Context.ShelterId : signal.ShelterId,
                signal.Category,
                signal.Key.Split(':')[0],
                signal.SourceModule,
                signal.EntityType,
                signal.EntityId,
                signal.EntityDisplayName,
                signal.Title,
                signal.Summary,
                severity,
                score,
                signal.ConfidenceLabel,
                explanation,
                signal.Evidence,
                signal.ScoreFactors,
                signal.RecommendedActions);
        }).ToList();

        return Task.FromResult<IReadOnlyCollection<OperationalInsightCandidate>>(candidates);
    }
}

