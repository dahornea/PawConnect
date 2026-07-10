using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class IntelligenceInsightService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IIntelligenceEngine engine,
    IIntelligenceRecommendationService recommendationService,
    IAuditLogService auditLogService,
    IOptions<IntelligenceHubOptions> options) : IIntelligenceInsightService
{
    private static readonly ConcurrentDictionary<string, DateTime> LastManualRefreshByScope = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntelligenceHubOptions settings = options.Value;

    public async Task<PagedResult<OperationalInsightDto>> GetInsightsAsync(
        IntelligenceScope scope,
        IntelligenceInsightQuery query,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = PagedResult<OperationalInsightDto>.Normalize(query.Page, query.PageSize);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var insights = ApplyScope(db.OperationalInsights.AsNoTracking(), scope);

        if (query.Severity.HasValue) insights = insights.Where(item => item.Severity == query.Severity);
        if (query.Category.HasValue) insights = insights.Where(item => item.Category == query.Category);
        if (query.Status.HasValue) insights = insights.Where(item => item.Status == query.Status);
        else insights = insights.Where(item => item.Status == IntelligenceInsightStatus.Active || item.Status == IntelligenceInsightStatus.Acknowledged || (query.IncludeSnoozed && item.Status == IntelligenceInsightStatus.Snoozed));
        if (!string.IsNullOrWhiteSpace(query.EntityType)) insights = insights.Where(item => item.EntityType == query.EntityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            insights = insights.Where(item => item.Title.Contains(search) || item.Summary.Contains(search) || (item.EntityDisplayName != null && item.EntityDisplayName.Contains(search)));
        }

        var total = await insights.CountAsync(cancellationToken);
        var entities = await insights.OrderByDescending(item => item.Severity)
            .ThenByDescending(item => item.PriorityScore)
            .ThenBy(item => item.FirstDetectedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var dtos = new List<OperationalInsightDto>(entities.Count);
        foreach (var entity in entities)
        {
            dtos.Add(await ToDtoAsync(entity, scope, cancellationToken));
        }

        return new PagedResult<OperationalInsightDto>(dtos, page, pageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<OperationalInsightDto?> GetInsightDetailsAsync(int insightId, IntelligenceScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var insight = await ApplyScope(db.OperationalInsights.AsNoTracking(), scope).FirstOrDefaultAsync(item => item.Id == insightId, cancellationToken);
        return insight is null ? null : await ToDtoAsync(insight, scope, cancellationToken);
    }

    public async Task<IntelligenceSummaryDto> GetSummaryAsync(IntelligenceScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var scoped = ApplyScope(db.OperationalInsights.AsNoTracking(), scope);
        var current = scoped.Where(item => item.Status == IntelligenceInsightStatus.Active || item.Status == IntelligenceInsightStatus.Acknowledged || item.Status == IntelligenceInsightStatus.Snoozed);
        var rows = await current.Select(item => new { item.Severity, item.Status, item.Category, item.LastEvaluatedAtUtc }).ToListAsync(cancellationToken);
        var resolved = await scoped.CountAsync(item => item.Status == IntelligenceInsightStatus.Resolved, cancellationToken);
        var weighted = rows.Sum(item => item.Severity switch
        {
            IntelligenceSeverity.Critical => 5,
            IntelligenceSeverity.High => 3,
            IntelligenceSeverity.Medium => 2,
            IntelligenceSeverity.Low => 1,
            _ => 0
        });
        var workload = weighted switch
        {
            >= 25 => "Critical",
            >= 15 => "High",
            >= 7 => "Elevated",
            _ => "Normal"
        };

        return new IntelligenceSummaryDto(
            rows.Count,
            rows.Count(item => item.Severity == IntelligenceSeverity.Critical),
            rows.Count(item => item.Severity == IntelligenceSeverity.High),
            rows.Count(item => item.Severity == IntelligenceSeverity.Medium),
            rows.Count(item => item.Status == IntelligenceInsightStatus.Acknowledged),
            rows.Count(item => item.Status == IntelligenceInsightStatus.Snoozed),
            resolved,
            rows.Select(item => (DateTime?)item.LastEvaluatedAtUtc).Max(),
            workload,
            rows.GroupBy(item => item.Category).ToDictionary(group => group.Key, group => group.Count()));
    }

    public Task AcknowledgeAsync(int insightId, IntelligenceScope scope, string actorUserId, CancellationToken cancellationToken = default)
        => UpdateLifecycleAsync(insightId, scope, actorUserId, AuditActions.IntelligenceInsightAcknowledged, cancellationToken, insight =>
        {
            insight.Status = IntelligenceInsightStatus.Acknowledged;
            insight.AcknowledgedAtUtc = DateTime.UtcNow;
            insight.AcknowledgedByUserId = actorUserId;
        });

    public Task SnoozeAsync(int insightId, IntelligenceScope scope, string actorUserId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(7)) throw new InvalidOperationException("Choose a snooze duration between one minute and seven days.");
        return UpdateLifecycleAsync(insightId, scope, actorUserId, AuditActions.IntelligenceInsightSnoozed, cancellationToken, insight =>
        {
            if (insight.Severity == IntelligenceSeverity.Critical && duration > TimeSpan.FromHours(1)) throw new InvalidOperationException("Critical insights can be snoozed for at most one hour.");
            insight.Status = IntelligenceInsightStatus.Snoozed;
            insight.SnoozedUntilUtc = DateTime.UtcNow.Add(duration);
        });
    }

    public Task ResolveAsync(int insightId, IntelligenceScope scope, string actorUserId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A short resolution reason is required.");
        return UpdateLifecycleAsync(insightId, scope, actorUserId, AuditActions.IntelligenceInsightManuallyResolved, cancellationToken, insight =>
        {
            if (insight.Severity == IntelligenceSeverity.Critical || insight.Category is IntelligenceCategory.Notifications or IntelligenceCategory.PlatformHealth)
            {
                throw new InvalidOperationException("Critical technical insights resolve automatically after the condition is fixed.");
            }
            insight.Status = IntelligenceInsightStatus.Resolved;
            insight.ResolvedAtUtc = DateTime.UtcNow;
            insight.ResolutionReason = reason.Trim().Length <= 500 ? reason.Trim() : reason.Trim()[..500];
        });
    }

    public Task ReopenAsync(int insightId, IntelligenceScope scope, string actorUserId, CancellationToken cancellationToken = default)
        => UpdateLifecycleAsync(insightId, scope, actorUserId, AuditActions.IntelligenceInsightReopened, cancellationToken, insight =>
        {
            insight.Status = IntelligenceInsightStatus.Active;
            insight.ResolvedAtUtc = null;
            insight.ResolutionReason = null;
            insight.SnoozedUntilUtc = null;
        });

    public async Task<IntelligenceEvaluationResult> RefreshAsync(IntelligenceScope scope, string actorUserId, CancellationToken cancellationToken = default)
    {
        var key = $"{scope.AudienceType}:{scope.ShelterId}:{scope.UserId}";
        var now = DateTime.UtcNow;
        if (LastManualRefreshByScope.TryGetValue(key, out var lastRefresh) && now - lastRefresh < TimeSpan.FromSeconds(Math.Max(5, settings.ManualRefreshCooldownSeconds)))
        {
            throw new InvalidOperationException("The intelligence view was refreshed recently. Please wait a moment before trying again.");
        }
        LastManualRefreshByScope[key] = now;

        var result = scope.AudienceType switch
        {
            IntelligenceAudienceType.Admin => await engine.EvaluateForAdminAsync(cancellationToken),
            IntelligenceAudienceType.Shelter when scope.ShelterId.HasValue => await engine.EvaluateForShelterAsync(scope.ShelterId.Value, cancellationToken),
            IntelligenceAudienceType.Adopter when !string.IsNullOrWhiteSpace(scope.UserId) => await engine.EvaluateForAdopterAsync(scope.UserId, cancellationToken),
            _ => throw new InvalidOperationException("A valid intelligence scope is required.")
        };
        await auditLogService.LogUserActionAsync(AuditActions.IntelligenceManualRefresh, "IntelligenceHub", key, "Manual intelligence refresh triggered.", new { result.SignalsCollected, result.Created, result.Updated, result.Resolved });
        return result;
    }

    private async Task UpdateLifecycleAsync(
        int insightId,
        IntelligenceScope scope,
        string actorUserId,
        string auditAction,
        CancellationToken cancellationToken,
        Action<OperationalInsight> update)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var insight = await ApplyScope(db.OperationalInsights, scope).FirstOrDefaultAsync(item => item.Id == insightId, cancellationToken)
            ?? throw new InvalidOperationException("Insight was not found or is outside your access scope.");
        update(insight);
        insight.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await auditLogService.LogUserActionAsync(auditAction, nameof(OperationalInsight), insight.Id.ToString(), $"Intelligence insight updated: {insight.Title}.", new { insight.Status, insight.SnoozedUntilUtc }, eventType: "Intelligence");
    }

    private async Task<OperationalInsightDto> ToDtoAsync(OperationalInsight insight, IntelligenceScope scope, CancellationToken cancellationToken)
    {
        var evidence = Deserialize<IReadOnlyList<string>>(insight.EvidenceJson, []);
        var scoreBreakdown = Deserialize<IReadOnlyList<IntelligenceScoreFactor>>(insight.ScoreBreakdownJson, []);
        var actions = Deserialize<IReadOnlyList<RecommendedActionDto>>(insight.RecommendedActionsJson, []);
        var validatedActions = await recommendationService.ValidateActionsAsync(actions, scope, cancellationToken);
        return new OperationalInsightDto(
            insight.Id, insight.AudienceType, insight.UserId, insight.ShelterId, insight.Category, insight.InsightType,
            insight.SourceModule, insight.EntityType, insight.EntityId, insight.EntityDisplayName, insight.Title, insight.Summary,
            insight.Severity, insight.PriorityScore, insight.ConfidenceLabel, insight.Explanation, evidence, scoreBreakdown,
            validatedActions, insight.Status, insight.FirstDetectedAtUtc, insight.LastDetectedAtUtc, insight.LastEvaluatedAtUtc,
            insight.AcknowledgedAtUtc, insight.SnoozedUntilUtc, insight.ResolvedAtUtc, insight.ResolutionReason);
    }

    private static IQueryable<OperationalInsight> ApplyScope(IQueryable<OperationalInsight> query, IntelligenceScope scope)
    {
        query = query.Where(insight => insight.AudienceType == scope.AudienceType);
        return scope.AudienceType switch
        {
            IntelligenceAudienceType.Shelter when scope.ShelterId.HasValue => query.Where(insight => insight.ShelterId == scope.ShelterId),
            IntelligenceAudienceType.Adopter when !string.IsNullOrWhiteSpace(scope.UserId) => query.Where(insight => insight.UserId == scope.UserId),
            IntelligenceAudienceType.Admin => query,
            _ => query.Where(_ => false)
        };
    }

    private static T Deserialize<T>(string json, T fallback)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback; }
        catch (JsonException) { return fallback; }
    }
}
