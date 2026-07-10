using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class IntelligenceEngine(
    IEnumerable<IIntelligenceSignalProvider> signalProviders,
    IEnumerable<IIntelligenceRule> rules,
    IDbContextFactory<ApplicationDbContext> contextFactory,
    INotificationService notificationService,
    IAuditLogService auditLogService,
    IOptions<IntelligenceHubOptions> options,
    ILogger<IntelligenceEngine> logger) : IIntelligenceEngine
{
    private static readonly SemaphoreSlim EvaluationLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntelligenceHubOptions settings = options.Value;

    public Task<IntelligenceEvaluationResult> EvaluateForShelterAsync(int shelterId, CancellationToken cancellationToken = default)
        => EvaluateAsync(new IntelligenceContext(IntelligenceAudienceType.Shelter, null, shelterId, DateTime.UtcNow), cancellationToken);

    public Task<IntelligenceEvaluationResult> EvaluateForAdminAsync(CancellationToken cancellationToken = default)
        => EvaluateAsync(new IntelligenceContext(IntelligenceAudienceType.Admin, null, null, DateTime.UtcNow), cancellationToken);

    public Task<IntelligenceEvaluationResult> EvaluateForAdopterAsync(string userId, CancellationToken cancellationToken = default)
        => EvaluateAsync(new IntelligenceContext(IntelligenceAudienceType.Adopter, userId, null, DateTime.UtcNow), cancellationToken);

    public async Task RefreshActiveInsightsAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
        {
            return;
        }

        await EvaluateForAdminAsync(cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var shelterIds = await db.Shelters.AsNoTracking().Select(shelter => shelter.Id).ToListAsync(cancellationToken);
        var adopterIds = await db.AdoptionRequests.AsNoTracking().Select(request => request.AdopterId)
            .Concat(db.SavedDogSearches.AsNoTracking().Select(search => search.AdopterUserId))
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var shelterId in shelterIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EvaluateForShelterAsync(shelterId, cancellationToken);
        }

        foreach (var adopterId in adopterIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EvaluateForAdopterAsync(adopterId, cancellationToken);
        }
    }

    private async Task<IntelligenceEvaluationResult> EvaluateAsync(IntelligenceContext context, CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return new IntelligenceEvaluationResult(context.AudienceType, context.UserId, context.ShelterId, 0, 0, 0, 0, 0, 0, context.UtcNow, TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();
        await EvaluationLock.WaitAsync(cancellationToken);
        try
        {
            var signals = new List<IntelligenceSignal>();
            var providerFailures = 0;
            var successfulProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var providers = signalProviders.ToList();
            foreach (var provider in providers)
            {
                try
                {
                    signals.AddRange(await provider.CollectSignalsAsync(context, cancellationToken));
                    successfulProviders.Add(provider.ProviderKey);
                }
                catch (Exception ex)
                {
                    providerFailures++;
                    logger.LogWarning(ex, "Intelligence provider {ProviderKey} failed for {AudienceType}.", provider.ProviderKey, context.AudienceType);
                }
            }

            var candidates = new List<OperationalInsightCandidate>();
            var ruleFailures = 0;
            foreach (var rule in rules)
            {
                try
                {
                    candidates.AddRange(await rule.EvaluateAsync(new IntelligenceEvaluationContext(context, signals), cancellationToken));
                }
                catch (Exception ex)
                {
                    ruleFailures++;
                    logger.LogWarning(ex, "Intelligence rule {RuleKey} failed for {AudienceType}.", rule.RuleKey, context.AudienceType);
                }
            }

            var deduplicated = candidates
                .GroupBy(candidate => candidate.Fingerprint, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(candidate => candidate.PriorityScore).First())
                .ToList();
            var reconciliation = await ReconcileAsync(context, deduplicated, successfulProviders, ruleFailures == 0, cancellationToken);
            stopwatch.Stop();

            logger.LogInformation(
                "Intelligence refresh for {AudienceType} collected {SignalCount} signals and created {Created}, updated {Updated}, resolved {Resolved} insights in {ElapsedMs} ms.",
                context.AudienceType, signals.Count, reconciliation.Created, reconciliation.Updated, reconciliation.Resolved, stopwatch.ElapsedMilliseconds);

            return new IntelligenceEvaluationResult(
                context.AudienceType, context.UserId, context.ShelterId, providers.Count, providerFailures, signals.Count,
                reconciliation.Created, reconciliation.Updated, reconciliation.Resolved, context.UtcNow, stopwatch.Elapsed);
        }
        finally
        {
            EvaluationLock.Release();
        }
    }

    private async Task<(int Created, int Updated, int Resolved)> ReconcileAsync(
        IntelligenceContext evaluationContext,
        IReadOnlyList<OperationalInsightCandidate> candidates,
        IReadOnlySet<string> successfulProviders,
        bool allowResolution,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existingQuery = db.OperationalInsights.Where(insight => insight.AudienceType == evaluationContext.AudienceType);
        existingQuery = evaluationContext.AudienceType switch
        {
            IntelligenceAudienceType.Shelter => existingQuery.Where(insight => insight.ShelterId == evaluationContext.ShelterId),
            IntelligenceAudienceType.Adopter => existingQuery.Where(insight => insight.UserId == evaluationContext.UserId),
            _ => existingQuery
        };

        var existing = await existingQuery.ToListAsync(cancellationToken);
        var byFingerprint = existing.ToDictionary(insight => insight.Fingerprint, StringComparer.OrdinalIgnoreCase);
        var activeFingerprints = candidates.Select(candidate => candidate.Fingerprint).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var updated = 0;
        var resolved = 0;
        var notifications = new List<OperationalInsight>();
        var auditEvents = new List<(string Action, OperationalInsight Insight, string Description)>();

        foreach (var candidate in candidates)
        {
            if (!byFingerprint.TryGetValue(candidate.Fingerprint, out var insight))
            {
                insight = new OperationalInsight { Fingerprint = candidate.Fingerprint, FirstDetectedAtUtc = evaluationContext.UtcNow, CreatedAtUtc = evaluationContext.UtcNow };
                ApplyCandidate(insight, candidate, evaluationContext.UtcNow);
                db.OperationalInsights.Add(insight);
                created++;
                auditEvents.Add((AuditActions.IntelligenceInsightGenerated, insight, $"Intelligence insight generated: {candidate.Title}."));
                if (candidate.PriorityScore >= settings.HighPriorityNotificationThreshold)
                {
                    notifications.Add(insight);
                }
                continue;
            }

            var previousSeverity = insight.Severity;
            var wasResolved = insight.Status is IntelligenceInsightStatus.Resolved or IntelligenceInsightStatus.Expired;
            ApplyCandidate(insight, candidate, evaluationContext.UtcNow);
            if (wasResolved || insight.Status == IntelligenceInsightStatus.Snoozed && insight.SnoozedUntilUtc <= evaluationContext.UtcNow)
            {
                insight.Status = IntelligenceInsightStatus.Active;
                insight.ResolvedAtUtc = null;
                insight.ResolutionReason = null;
                insight.SnoozedUntilUtc = null;
                auditEvents.Add((AuditActions.IntelligenceInsightReopened, insight, $"Intelligence insight reopened: {candidate.Title}."));
            }
            else if (candidate.Severity > previousSeverity)
            {
                insight.Status = IntelligenceInsightStatus.Active;
                auditEvents.Add((AuditActions.IntelligenceSeverityEscalated, insight, $"Intelligence insight severity escalated: {candidate.Title}."));
                if (candidate.PriorityScore >= settings.HighPriorityNotificationThreshold)
                {
                    notifications.Add(insight);
                }
            }
            updated++;
        }

        foreach (var insight in existing.Where(insight =>
                     allowResolution &&
                     successfulProviders.Contains(insight.SourceModule) &&
                     !activeFingerprints.Contains(insight.Fingerprint) &&
                     insight.Status is IntelligenceInsightStatus.Active or IntelligenceInsightStatus.Acknowledged or IntelligenceInsightStatus.Snoozed))
        {
            insight.Status = IntelligenceInsightStatus.Resolved;
            insight.ResolvedAtUtc = evaluationContext.UtcNow;
            insight.ResolutionReason = "The latest evaluation no longer found the triggering condition.";
            insight.LastEvaluatedAtUtc = evaluationContext.UtcNow;
            insight.UpdatedAtUtc = evaluationContext.UtcNow;
            resolved++;
            auditEvents.Add((AuditActions.IntelligenceInsightAutoResolved, insight, $"Intelligence insight automatically resolved: {insight.Title}."));
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var auditEvent in auditEvents)
        {
            await auditLogService.LogSystemEventAsync(auditEvent.Action, nameof(OperationalInsight), auditEvent.Insight.Id.ToString(), auditEvent.Description,
                new { auditEvent.Insight.Fingerprint, auditEvent.Insight.PriorityScore, Severity = auditEvent.Insight.Severity.ToString() });
        }
        foreach (var insight in notifications)
        {
            await NotifyAudienceAsync(db, insight);
        }

        return (created, updated, resolved);
    }

    private static void ApplyCandidate(OperationalInsight insight, OperationalInsightCandidate candidate, DateTime now)
    {
        insight.AudienceType = candidate.AudienceType;
        insight.UserId = candidate.UserId;
        insight.ShelterId = candidate.ShelterId;
        insight.Category = candidate.Category;
        insight.InsightType = Truncate(candidate.InsightType, 80);
        insight.SourceModule = Truncate(candidate.SourceModule, 80);
        insight.EntityType = Truncate(candidate.EntityType, 80);
        insight.EntityId = TruncateOptional(candidate.EntityId, 80);
        insight.EntityDisplayName = TruncateOptional(candidate.EntityDisplayName, 160);
        insight.Title = Truncate(candidate.Title, 180);
        insight.Summary = Truncate(candidate.Summary, 500);
        insight.Severity = candidate.Severity;
        insight.PriorityScore = candidate.PriorityScore;
        insight.ConfidenceLabel = Truncate(candidate.ConfidenceLabel, 40);
        insight.Explanation = Truncate(candidate.Explanation, 2000);
        insight.EvidenceJson = Truncate(JsonSerializer.Serialize(candidate.Evidence, JsonOptions), 8000);
        insight.ScoreBreakdownJson = Truncate(JsonSerializer.Serialize(candidate.ScoreBreakdown, JsonOptions), 4000);
        insight.RecommendedActionsJson = Truncate(JsonSerializer.Serialize(candidate.RecommendedActions, JsonOptions), 4000);
        insight.LastDetectedAtUtc = now;
        insight.LastEvaluatedAtUtc = now;
        insight.UpdatedAtUtc = now;
    }

    private async Task NotifyAudienceAsync(ApplicationDbContext db, OperationalInsight insight)
    {
        var userIds = new List<string>();
        if (insight.AudienceType == IntelligenceAudienceType.Adopter && !string.IsNullOrWhiteSpace(insight.UserId))
        {
            userIds.Add(insight.UserId);
        }
        else if (insight.AudienceType == IntelligenceAudienceType.Shelter && insight.ShelterId.HasValue)
        {
            var shelterUserId = await db.Shelters.AsNoTracking().Where(shelter => shelter.Id == insight.ShelterId).Select(shelter => shelter.ApplicationUserId).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(shelterUserId)) userIds.Add(shelterUserId);
        }
        else if (insight.AudienceType == IntelligenceAudienceType.Admin)
        {
            var adminRoleId = await db.Roles.AsNoTracking().Where(role => role.Name == IdentitySeedData.AdminRole).Select(role => role.Id).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(adminRoleId)) userIds.AddRange(await db.UserRoles.AsNoTracking().Where(item => item.RoleId == adminRoleId).Select(item => item.UserId).ToListAsync());
        }

        var link = insight.AudienceType switch
        {
            IntelligenceAudienceType.Shelter => "/shelter/intelligence",
            IntelligenceAudienceType.Adopter => "/adopter/insights",
            _ => "/admin/intelligence"
        };
        foreach (var userId in userIds.Distinct(StringComparer.Ordinal))
        {
            await notificationService.CreateNotificationAsync(userId, insight.Title, insight.Summary, NotificationCategory.System,
                insight.Severity >= IntelligenceSeverity.Critical ? NotificationType.Error : NotificationType.Warning,
                link, nameof(OperationalInsight), insight.Id.ToString(), TimeSpan.FromHours(24));
        }
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
    private static string? TruncateOptional(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), maxLength);
}
