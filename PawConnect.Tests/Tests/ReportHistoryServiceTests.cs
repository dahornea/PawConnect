using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class ReportHistoryServiceTests
{
    [Fact]
    public async Task RecordReportSentAsync_CreatesMetadataOnlyRecord()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = CreateService(databaseName);

        await service.RecordReportSentAsync(new ReportHistoryRecord(
            ReportHistoryTypes.ShelterSummaryReport,
            ReportHistoryTriggers.Manual,
            "shelter@test.com",
            "Report subject",
            "ShelterSummaryReport.pdf",
            ShelterId: TestDbContextFactory.ShelterId));

        var history = Assert.Single(context.ReportHistories);
        Assert.Equal(ReportHistoryTypes.ShelterSummaryReport, history.ReportType);
        Assert.Equal("shelter@test.com", history.RecipientEmail);
        Assert.Equal("ShelterSummaryReport.pdf", history.FileName);
        Assert.True(history.WasSuccessful);
        Assert.NotNull(history.SentAt);
        Assert.DoesNotContain(
            typeof(ReportHistory).GetProperties(),
            property => property.Name.Contains("Bytes", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Content", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetReportHistoryForShelterAsync_ReturnsOnlyCurrentShelterRecords()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        TestDbContextFactory.CreateContext(databaseName).Dispose();
        var service = CreateService(databaseName);

        await service.RecordReportGeneratedAsync(new ReportHistoryRecord(
            ReportHistoryTypes.CsvExport,
            ReportHistoryTriggers.Shelter,
            FileName: "shelter-dogs.csv",
            ShelterId: TestDbContextFactory.ShelterId));
        await service.RecordReportGeneratedAsync(new ReportHistoryRecord(
            ReportHistoryTypes.CsvExport,
            ReportHistoryTriggers.Shelter,
            FileName: "other-shelter-dogs.csv",
            ShelterId: TestDbContextFactory.OtherShelterId));

        var currentShelterHistory = await service.GetReportHistoryForShelterAsync(TestDbContextFactory.ShelterId);

        var history = Assert.Single(currentShelterHistory);
        Assert.Equal("shelter-dogs.csv", history.FileName);
    }

    [Fact]
    public async Task GetAdminReportHistoryAsync_ReturnsAllRecordsAndCanFilterFailures()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        TestDbContextFactory.CreateContext(databaseName).Dispose();
        var service = CreateService(databaseName);

        await service.RecordReportGeneratedAsync(new ReportHistoryRecord(
            ReportHistoryTypes.CsvExport,
            ReportHistoryTriggers.Admin,
            FileName: "users.csv"));
        await service.RecordReportFailedAsync(new ReportHistoryRecord(
            ReportHistoryTypes.ShelterSummaryReport,
            ReportHistoryTriggers.Quartz,
            "shelter@test.com",
            "Summary",
            "summary.pdf",
            ErrorMessage: "SMTP unavailable",
            ShelterId: TestDbContextFactory.ShelterId));

        var allReports = await service.GetAdminReportHistoryAsync();
        var failedReports = await service.GetAdminReportHistoryAsync(wasSuccessful: false);

        Assert.Equal(2, allReports.Count);
        var failed = Assert.Single(failedReports);
        Assert.Equal("SMTP unavailable", failed.ErrorMessage);
        Assert.False(failed.WasSuccessful);
    }

    [Fact]
    public async Task SendShelterSummaryReportAsync_RecordsSuccessfulManualReport()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var historyService = CreateService(databaseName);
        var emailService = new TestEmailService();
        var summaryService = CreateShelterSummaryReportService(context, emailService, historyService);

        await summaryService.SendShelterSummaryReportAsync(TestDbContextFactory.ShelterId);

        var history = Assert.Single(await historyService.GetReportHistoryForShelterAsync(TestDbContextFactory.ShelterId));
        Assert.Equal(ReportHistoryTypes.ShelterSummaryReport, history.ReportType);
        Assert.Equal(ReportHistoryTriggers.Manual, history.TriggeredBy);
        Assert.Equal("shelter@test.com", history.RecipientEmail);
        Assert.True(history.WasSuccessful);
        Assert.NotNull(history.SentAt);
    }

    [Fact]
    public async Task SendShelterSummaryReportAsync_RecordsFailedSend()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var historyService = CreateService(databaseName);
        var summaryService = CreateShelterSummaryReportService(context, new FailingEmailService(), historyService);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            summaryService.SendShelterSummaryReportAsync(TestDbContextFactory.ShelterId));

        var history = Assert.Single(await historyService.GetReportHistoryForShelterAsync(TestDbContextFactory.ShelterId));
        Assert.Equal(ReportHistoryTypes.ShelterSummaryReport, history.ReportType);
        Assert.False(history.WasSuccessful);
        Assert.Contains("SMTP unavailable", history.ErrorMessage);
    }

    [Fact]
    public async Task SendScheduledShelterSummaryReportsAsync_RecordsQuartzTrigger()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var historyService = CreateService(databaseName);
        var emailService = new TestEmailService();
        var summaryService = CreateShelterSummaryReportService(context, emailService, historyService, enabled: true);

        var sentCount = await summaryService.SendScheduledShelterSummaryReportsAsync();

        var history = await historyService.GetAdminReportHistoryAsync(ReportHistoryTypes.ShelterSummaryReport);
        Assert.Equal(2, sentCount);
        Assert.Equal(2, history.Count);
        Assert.All(history, report => Assert.Equal(ReportHistoryTriggers.Quartz, report.TriggeredBy));
    }

    [Theory]
    [InlineData(ReportHistoryTriggers.Quartz, "Scheduled report")]
    [InlineData(ReportHistoryTriggers.Manual, "Sent manually")]
    [InlineData(ReportHistoryTriggers.System, "System")]
    [InlineData(ReportHistoryTriggers.Admin, "Admin")]
    [InlineData(ReportHistoryTriggers.Shelter, "Shelter")]
    public void ReportHistoryTriggerLabels_Format_ReturnsUserFriendlyLabels(string trigger, string expectedLabel)
    {
        Assert.Equal(expectedLabel, ReportHistoryTriggerLabels.Format(trigger));
    }

    [Fact]
    public async Task ExportGeneration_CreatesReportHistoryRecord()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var historyService = CreateService(databaseName);
        var exportService = new ExportService(
            context,
            TestDbContextFactory.CreateUserManager(context),
            NullLogger<ExportService>.Instance,
            reportHistoryService: historyService);

        var file = await exportService.GenerateShelterDogsCsvAsync(TestDbContextFactory.ShelterId);

        var history = Assert.Single(await historyService.GetReportHistoryForShelterAsync(TestDbContextFactory.ShelterId));
        Assert.Equal(ReportHistoryTypes.CsvExport, history.ReportType);
        Assert.Equal(file.FileName, history.FileName);
        Assert.Equal(ReportHistoryTriggers.Shelter, history.TriggeredBy);
        Assert.True(history.WasSuccessful);
        Assert.Null(history.SentAt);
    }

    private static ReportHistoryService CreateService(string databaseName)
    {
        return new ReportHistoryService(
            TestDbContextFactory.CreateContextFactory(databaseName),
            new HttpContextAccessor(),
            NullLogger<ReportHistoryService>.Instance);
    }

    private static ShelterSummaryReportService CreateShelterSummaryReportService(
        ApplicationDbContext context,
        IEmailService emailService,
        IReportHistoryService historyService,
        bool enabled = true)
    {
        return new ShelterSummaryReportService(
            context,
            emailService,
            new TestPdfReportService(),
            Options.Create(new ScheduledReportSettings { Enabled = enabled, ShelterReportIntervalMinutes = 5 }),
            NullLogger<ShelterSummaryReportService>.Instance,
            reportHistoryService: historyService);
    }

    private sealed class FailingEmailService : IEmailService
    {
        public Task SendEmailAsync(string to, string subject, string body, List<EmailAttachment>? attachments = null, string? htmlBody = null)
        {
            throw new InvalidOperationException("SMTP unavailable");
        }
    }
}
