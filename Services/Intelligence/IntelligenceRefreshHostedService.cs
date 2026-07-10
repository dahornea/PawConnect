using Microsoft.Extensions.Options;

namespace PawConnect.Services.Intelligence;

public sealed class IntelligenceRefreshHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<IntelligenceHubOptions> options,
    ILogger<IntelligenceRefreshHostedService> logger) : BackgroundService
{
    private readonly IntelligenceHubOptions settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.GetSafeRefreshMinutes()));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IIntelligenceEngine>().RefreshActiveInsightsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "The Intelligence Hub background refresh failed. The next scheduled refresh will still run.");
            }
        }
    }
}
