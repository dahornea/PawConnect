using Microsoft.Extensions.Options;

namespace PawConnect.Services;

public class NotificationOutboxHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOutboxSettings> options,
    ILogger<NotificationOutboxHostedService> logger) : BackgroundService
{
    private readonly NotificationOutboxSettings settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            logger.LogInformation("Notification outbox processor is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.GetSafePollIntervalSeconds()));

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<INotificationOutboxProcessor>();
            var result = await processor.ProcessDueAsync(settings.GetSafeBatchSize(), cancellationToken);

            if (result.Processed > 0)
            {
                logger.LogInformation(
                    "Notification outbox processed {Processed} messages. Sent: {Sent}, failed: {Failed}, dead-lettered: {DeadLettered}, skipped: {Skipped}.",
                    result.Processed,
                    result.Sent,
                    result.Failed,
                    result.DeadLettered,
                    result.Skipped);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification outbox processor failed.");
        }
    }
}
