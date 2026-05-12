using Microsoft.Extensions.Options;
using PawConnect.Services;
using Quartz;

namespace PawConnect.Jobs;

public class ShelterSummaryReportJob(
    IShelterSummaryReportService reportService,
    IOptions<ScheduledReportSettings> options,
    ILogger<ShelterSummaryReportJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Shelter summary report job skipped because scheduled reports are disabled.");
            return;
        }

        try
        {
            logger.LogInformation("Shelter summary report job started.");
            var sentCount = await reportService.SendScheduledShelterSummaryReportsAsync(context.CancellationToken);
            logger.LogInformation("Shelter summary report job completed. Reports sent: {ReportCount}.", sentCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shelter summary report job failed.");
        }
    }
}
