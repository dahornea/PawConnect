using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class NotificationReliabilitySignalProvider(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IOptions<IntelligenceHubOptions> options) : IIntelligenceSignalProvider
{
    private readonly IntelligenceHubOptions settings = options.Value;

    public string ProviderKey => "NotificationReliability";

    public async Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(IntelligenceContext context, CancellationToken cancellationToken)
    {
        if (context.AudienceType != IntelligenceAudienceType.Admin)
        {
            return [];
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var summary = await db.NotificationOutboxMessages.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Failed = group.Count(message => message.Status == NotificationOutboxStatus.Failed),
                DeadLetter = group.Count(message => message.Status == NotificationOutboxStatus.DeadLetter),
                Pending = group.Count(message => message.Status == NotificationOutboxStatus.Pending),
                OldestPending = group.Where(message => message.Status == NotificationOutboxStatus.Pending).Min(message => (DateTime?)message.CreatedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (summary is null || (summary.Failed < settings.OutboxFailedWarningCount && summary.DeadLetter < settings.OutboxDeadLetterCriticalCount))
        {
            return [];
        }

        var evidence = new List<string> { $"Failed: {summary.Failed}", $"Dead letter: {summary.DeadLetter}", $"Pending: {summary.Pending}" };
        if (summary.OldestPending.HasValue)
        {
            evidence.Add($"Oldest pending: {summary.OldestPending.Value:dd MMM yyyy HH:mm}");
        }

        return
        [
            new IntelligenceSignal(
                "NotificationReliability:Platform", IntelligenceCategory.Notifications, ProviderKey, "NotificationOutbox", null, "Platform notification queue", null, null,
                "Notification delivery needs attention",
                $"The outbox contains {summary.Failed} failed and {summary.DeadLetter} dead-letter message(s).",
                "Unresolved delivery failures can prevent users from receiving time-sensitive updates, even though core database workflows continue.",
                "failed and dead-letter counts fall below the configured thresholds",
                $"Failed >= {settings.OutboxFailedWarningCount} or dead letter >= {settings.OutboxDeadLetterCriticalCount}",
                evidence,
                [new("Delivery failures", Math.Min(40, summary.Failed * 6), $"{summary.Failed} failed messages."), new("Dead-letter impact", Math.Min(42, summary.DeadLetter * 22), $"{summary.DeadLetter} messages exhausted retries."), new("User impact", 20, "Notifications may not reach intended recipients.")],
                [new("open-outbox", "Review notification outbox", "Filter failed and dead-letter messages, then retry eligible items.", "Navigate", "/admin/notification-outbox?status=Failed", "Admin", "NotificationOutbox", null, true)],
                context.UtcNow)
        ];
    }
}
