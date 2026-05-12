using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ShelterSummaryReportService(
    ApplicationDbContext context,
    IEmailService emailService,
    IPdfReportService pdfReportService,
    IOptions<ScheduledReportSettings> options,
    ILogger<ShelterSummaryReportService> logger) : IShelterSummaryReportService
{
    private readonly ScheduledReportSettings settings = options.Value;

    public async Task SendShelterSummaryReportAsync(int shelterId, CancellationToken cancellationToken = default)
    {
        var shelter = await context.Shelters
            .Include(s => s.ApplicationUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shelterId, cancellationToken);

        if (shelter is null)
        {
            throw new InvalidOperationException("Shelter was not found.");
        }

        var toDate = DateTime.UtcNow;
        var fromDate = toDate.AddDays(-1);
        await SendReportForShelterAsync(shelter, fromDate, toDate, cancellationToken);
    }

    public async Task<int> SendScheduledShelterSummaryReportsAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
        {
            logger.LogInformation("Scheduled shelter summary reports are disabled.");
            return 0;
        }

        var toDate = DateTime.UtcNow;
        var fromDate = toDate.AddMinutes(-settings.GetSafeShelterReportIntervalMinutes());
        var shelters = await context.Shelters
            .Include(s => s.ApplicationUser)
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Scheduled shelter summary report run found {ShelterCount} shelters.", shelters.Count);

        var sentCount = 0;
        for (var index = 0; index < shelters.Count; index++)
        {
            var shelter = shelters[index];
            try
            {
                await SendReportForShelterAsync(shelter, fromDate, toDate, cancellationToken);
                sentCount++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Scheduled shelter summary report failed for shelter {ShelterId}.", shelter.Id);
            }
        }

        return sentCount;
    }

    private async Task SendReportForShelterAsync(
        Shelter shelter,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        var recipient = shelter.ApplicationUser?.Email ?? shelter.Email;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new InvalidOperationException("Shelter email is not available.");
        }

        logger.LogInformation(
            "Sending shelter summary report for shelter {ShelterId} ({ShelterName}) to {Recipient}.",
            shelter.Id,
            shelter.Name,
            recipient);

        var pdfBytes = await pdfReportService.GenerateShelterSummaryReportAsync(shelter.Id, fromDate, toDate);
        var fileDate = toDate.ToLocalTime().ToString("yyyy-MM-dd");
        var attachments = new List<EmailAttachment>
        {
            new()
            {
                FileName = $"ShelterSummaryReport-{fileDate}.pdf",
                ContentType = "application/pdf",
                Content = pdfBytes
            }
        };

        var subject = $"PawConnect Shelter Summary Report - {fileDate}";
        var body = $"""
            Hello,

            Your PawConnect shelter summary report is attached.

            Shelter: {shelter.Name}
            Report period: {fromDate.ToLocalTime():dd MMM yyyy HH:mm} - {toDate.ToLocalTime():dd MMM yyyy HH:mm}

            This report summarizes adoption requests, dog statuses, and low-stock resources.
            """;

        var htmlBody = PawConnectEmailTemplate.BuildHtml(
            "Shelter Summary Report",
            "Hello,",
            [
                "Your PawConnect shelter summary report is attached.",
                "It summarizes adoption requests, dog statuses, and low-stock resources for your shelter."
            ],
            details:
            [
                new("Shelter", shelter.Name),
                new("Report period", $"{fromDate.ToLocalTime():dd MMM yyyy HH:mm} - {toDate.ToLocalTime():dd MMM yyyy HH:mm}")
            ],
            hasAttachment: true,
            note: "This report was generated automatically by PawConnect.");

        cancellationToken.ThrowIfCancellationRequested();
        await emailService.SendEmailAsync(recipient, subject, body, attachments, htmlBody);
        logger.LogInformation("Shelter summary report sent to shelter {ShelterId}.", shelter.Id);
    }
}
