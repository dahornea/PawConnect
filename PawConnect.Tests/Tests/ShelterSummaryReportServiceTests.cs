using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class ShelterSummaryReportServiceTests
{
    [Fact]
    public async Task SendShelterSummaryReportAsync_SendsPdfAttachmentToCurrentShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService);

        await service.SendShelterSummaryReportAsync(TestDbContextFactory.ShelterId);

        var email = Assert.Single(emailService.SentEmails);
        Assert.Equal("shelter@test.com", email.To);
        Assert.Contains("Shelter Summary Report", email.Subject);
        var attachment = Assert.Single(email.Attachments!);
        Assert.StartsWith("ShelterSummaryReport-", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.NotEmpty(attachment.Content);
    }

    [Fact]
    public async Task SendScheduledShelterSummaryReportsAsync_RespectsDisabledConfiguration()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedPendingRequest(context);
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService, enabled: false);

        var sentCount = await service.SendScheduledShelterSummaryReportsAsync();

        Assert.Equal(0, sentCount);
        Assert.Empty(emailService.SentEmails);
    }

    [Fact]
    public async Task SendScheduledShelterSummaryReportsAsync_SendsAllSheltersWithEmail()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedPendingRequest(context);
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService, enabled: true);

        var sentCount = await service.SendScheduledShelterSummaryReportsAsync();

        Assert.Equal(2, sentCount);
        Assert.Contains(emailService.SentEmails, email => email.To == "shelter@test.com");
        Assert.Contains(emailService.SentEmails, email => email.To == "other-shelter@test.com");
    }

    [Fact]
    public async Task GenerateShelterSummaryReportAsync_ReturnsPdfBytes()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedPendingRequest(context);
        context.ResourceStocks.Add(new ResourceStock
        {
            ShelterId = TestDbContextFactory.ShelterId,
            Name = "Adult Food",
            Quantity = 2,
            Unit = "kg",
            LowStockThreshold = 5,
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            FoodTypeId = TestDbContextFactory.AdultFoodTypeId
        });
        await context.SaveChangesAsync();
        var pdfService = new PdfReportService(context, NullLogger<PdfReportService>.Instance);

        var pdf = await pdfService.GenerateShelterSummaryReportAsync(
            TestDbContextFactory.ShelterId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow);

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, Math.Min(4, pdf.Length)));
    }

    private static ShelterSummaryReportService CreateService(
        ApplicationDbContext context,
        TestEmailService emailService,
        bool enabled = true)
    {
        return new ShelterSummaryReportService(
            context,
            emailService,
            new TestPdfReportService(),
            Options.Create(new ScheduledReportSettings
            {
                Enabled = enabled,
                ShelterReportIntervalMinutes = 5
            }),
            NullLogger<ShelterSummaryReportService>.Instance);
    }

    private static AdoptionRequest SeedPendingRequest(ApplicationDbContext context)
    {
        var dog = TestDbContextFactory.CreateDog("Report Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        context.SaveChanges();

        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            Dog = dog,
            AdopterId = TestDbContextFactory.AdopterId,
            ReasonForAdoption = "I can provide a stable home.",
            Status = AdoptionRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AdoptionRequests.Add(request);
        context.SaveChanges();
        return request;
    }
}
