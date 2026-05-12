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
    ILogger<ShelterSummaryReportService> logger,
    IAuditLogService? auditLogService = null,
    INotificationService? notificationService = null,
    IReportHistoryService? reportHistoryService = null) : IShelterSummaryReportService
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
        await SendReportForShelterAsync(
            shelter,
            fromDate,
            toDate,
            ReportHistoryTriggers.Manual,
            suppressReportNotificationDuplicates: false,
            cancellationToken);
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
                await SendReportForShelterAsync(
                    shelter,
                    fromDate,
                    toDate,
                    ReportHistoryTriggers.Quartz,
                    suppressReportNotificationDuplicates: true,
                    cancellationToken);
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
        string triggeredBy,
        bool suppressReportNotificationDuplicates,
        CancellationToken cancellationToken)
    {
        var recipient = shelter.ApplicationUser?.Email ?? shelter.Email;
        var fileDate = toDate.ToLocalTime().ToString("yyyy-MM-dd");
        var fileName = $"ShelterSummaryReport-{fileDate}.pdf";
        var subject = $"PawConnect Shelter Summary Report - {fileDate}";
        var generatedAt = DateTime.UtcNow;

        try
        {
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
            var attachments = new List<EmailAttachment>
            {
                new()
                {
                    FileName = fileName,
                    ContentType = "application/pdf",
                    Content = pdfBytes
                }
            };

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
            await RecordShelterSummaryReportSentAsync(
                shelter,
                recipient,
                subject,
                fileName,
                generatedAt,
                triggeredBy);
        }
        catch (Exception ex)
        {
            await RecordShelterSummaryReportFailedAsync(
                shelter,
                recipient,
                subject,
                fileName,
                generatedAt,
                triggeredBy,
                ex);
            throw;
        }

        if (notificationService is not null && !string.IsNullOrWhiteSpace(shelter.ApplicationUserId))
        {
            await notificationService.CreateNotificationAsync(
                shelter.ApplicationUserId,
                "Summary report sent",
                "Your shelter summary report was sent by email.",
                NotificationCategory.Report,
                NotificationType.Success,
                "/shelter/dashboard",
                "Shelter",
                shelter.Id.ToString(),
                suppressReportNotificationDuplicates ? TimeSpan.FromMinutes(60) : null);
        }
        if (auditLogService is not null)
        {
            await auditLogService.LogSystemAsync(
                AuditActions.ReportGenerated,
                "Shelter",
                shelter.Id.ToString(),
                $"Shelter summary report was sent to {shelter.Name}.",
                additionalData: $"Recipient={recipient};From={fromDate:O};To={toDate:O}");
        }
        logger.LogInformation("Shelter summary report sent to shelter {ShelterId}.", shelter.Id);
    }

    private Task RecordShelterSummaryReportSentAsync(
        Shelter shelter,
        string recipient,
        string subject,
        string fileName,
        DateTime generatedAt,
        string triggeredBy)
    {
        return reportHistoryService?.RecordReportSentAsync(new ReportHistoryRecord(
            ReportHistoryTypes.ShelterSummaryReport,
            triggeredBy,
            recipient,
            subject,
            fileName,
            generatedAt,
            SentAt: DateTime.UtcNow,
            ShelterId: shelter.Id,
            RelatedEntityName: "Shelter",
            RelatedEntityId: shelter.Id.ToString())) ?? Task.CompletedTask;
    }

    private Task RecordShelterSummaryReportFailedAsync(
        Shelter shelter,
        string? recipient,
        string subject,
        string fileName,
        DateTime generatedAt,
        string triggeredBy,
        Exception exception)
    {
        return reportHistoryService?.RecordReportFailedAsync(new ReportHistoryRecord(
            ReportHistoryTypes.ShelterSummaryReport,
            triggeredBy,
            recipient,
            subject,
            fileName,
            generatedAt,
            ErrorMessage: exception.Message,
            ShelterId: shelter.Id,
            RelatedEntityName: "Shelter",
            RelatedEntityId: shelter.Id.ToString())) ?? Task.CompletedTask;
    }
}
