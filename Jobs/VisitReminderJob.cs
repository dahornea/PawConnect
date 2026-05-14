using Microsoft.Extensions.Options;
using PawConnect.Services;
using Quartz;

namespace PawConnect.Jobs;

[DisallowConcurrentExecution]
public class VisitReminderJob(
    IVisitReminderService visitReminderService,
    IOptions<VisitReminderSettings> options,
    ILogger<VisitReminderJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Visit reminder job skipped because visit reminders are disabled.");
            return;
        }

        try
        {
            logger.LogInformation("Visit reminder job started.");
            var sentCount = await visitReminderService.SendDueVisitRemindersAsync(context.CancellationToken);
            logger.LogInformation("Visit reminder job completed. Reminders sent: {ReminderCount}.", sentCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Visit reminder job failed.");
        }
    }
}
