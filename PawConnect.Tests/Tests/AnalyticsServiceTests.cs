using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class AnalyticsServiceTests
{
    [Fact]
    public async Task GetAdminAnalyticsAsync_IncludesPlatformDataAndSupportsShelterFilter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var ownDog = TestDbContextFactory.CreateDog("Own Shelter Dog", DogStatus.Available, TestDbContextFactory.ShelterId);
        var otherDog = TestDbContextFactory.CreateDog("Other Shelter Dog", DogStatus.Reserved, TestDbContextFactory.OtherShelterId);
        context.Dogs.AddRange(ownDog, otherDog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.AddRange(
            Request(ownDog.Id, AdoptionRequestStatus.Pending, CreatedAt(1)),
            Request(otherDog.Id, AdoptionRequestStatus.Accepted, CreatedAt(2), UpdatedAt(4)));
        await context.SaveChangesAsync();
        var service = new AnalyticsService(context);

        var platform = await service.GetAdminAnalyticsAsync(JanuaryRange(), null, TestDbContextFactory.AdminId);
        var shelterOnly = await service.GetAdminAnalyticsAsync(JanuaryRange(), TestDbContextFactory.ShelterId, TestDbContextFactory.AdminId);

        Assert.Equal(2, platform.AdoptionFunnel.SubmittedRequests);
        Assert.Equal(50, platform.AdoptionFunnel.ConversionRate);
        Assert.Equal(1, shelterOnly.AdoptionFunnel.SubmittedRequests);
        Assert.Single(shelterOnly.ShelterWorkload);
        Assert.Equal(TestDbContextFactory.ShelterId, shelterOnly.ShelterWorkload[0].ShelterId);
    }

    [Fact]
    public async Task GetShelterAnalyticsAsync_UsesOnlyCurrentShelterData()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var ownDog = TestDbContextFactory.CreateDog("Own Visible Dog", DogStatus.Available, TestDbContextFactory.ShelterId);
        var otherDog = TestDbContextFactory.CreateDog("Other Visible Dog", DogStatus.Available, TestDbContextFactory.OtherShelterId);
        context.Dogs.AddRange(ownDog, otherDog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.AddRange(
            Request(ownDog.Id, AdoptionRequestStatus.Pending, CreatedAt(3)),
            Request(otherDog.Id, AdoptionRequestStatus.Pending, CreatedAt(3), adopterId: TestDbContextFactory.SecondAdopterId));
        context.RecentlyViewedDogs.AddRange(
            new RecentlyViewedDog { DogId = ownDog.Id, AdopterId = TestDbContextFactory.AdopterId, ViewedAt = CreatedAt(4) },
            new RecentlyViewedDog { DogId = otherDog.Id, AdopterId = TestDbContextFactory.SecondAdopterId, ViewedAt = CreatedAt(4) });
        context.ResourceStocks.AddRange(
            Resource("Own low food", TestDbContextFactory.ShelterId, quantity: 2, threshold: 5),
            Resource("Other low food", TestDbContextFactory.OtherShelterId, quantity: 1, threshold: 5));
        await context.SaveChangesAsync();
        var service = new AnalyticsService(context);

        var dashboard = await service.GetShelterAnalyticsAsync(JanuaryRange(), TestDbContextFactory.ShelterUserId);

        Assert.Equal(1, dashboard.AdoptionFunnel.SubmittedRequests);
        Assert.Equal(1, dashboard.ResourceAnalytics.LowStockResources);
        Assert.Single(dashboard.MostViewedDogs);
        Assert.Equal("Own Visible Dog", dashboard.MostViewedDogs[0].DogName);
        Assert.DoesNotContain(dashboard.MostViewedDogs, dog => dog.ShelterName == "Other Shelter");
    }

    [Fact]
    public async Task GetShelterAnalyticsAsync_BlocksNonShelterUsers()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AnalyticsService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetShelterAnalyticsAsync(JanuaryRange(), TestDbContextFactory.AdopterId));

        Assert.Equal("Only shelter accounts can access shelter analytics.", exception.Message);
    }

    [Fact]
    public async Task GetAdminAnalyticsAsync_RejectsInvalidDateRange()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AnalyticsService(context);
        var invalidRange = new AnalyticsDateRange(CreatedAt(10), CreatedAt(1), "Invalid");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAdminAnalyticsAsync(invalidRange, null, TestDbContextFactory.AdminId));
    }

    [Fact]
    public async Task GetAdminAnalyticsAsync_AggregatesResourcesReportsAndCopilotHistory()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.AddRange(
            Resource("Critical food", TestDbContextFactory.ShelterId, quantity: 1, threshold: 5),
            Resource("Healthy medicine", TestDbContextFactory.ShelterId, quantity: 20, threshold: 5, TestDbContextFactory.MedicineCategoryId));
        context.ReportHistories.AddRange(
            new ReportHistory
            {
                ReportType = ReportHistoryTypes.ShelterSummaryReport,
                TriggeredBy = ReportHistoryTriggers.Shelter,
                WasSuccessful = true,
                SentAt = CreatedAt(5),
                GeneratedAt = CreatedAt(5),
                ShelterId = TestDbContextFactory.ShelterId
            },
            new ReportHistory
            {
                ReportType = ReportHistoryTypes.CsvExport,
                TriggeredBy = ReportHistoryTriggers.Admin,
                WasSuccessful = false,
                GeneratedAt = CreatedAt(6)
            });
        var copilotSession = new CopilotSession
        {
            AdopterUserId = TestDbContextFactory.AdopterId,
            CreatedAt = CreatedAt(7),
            QueryText = "raw adopter prompt stays out of analytics dto",
            PrimaryIntent = "Apartment",
            UsedAiEnhancement = true,
            UsedSemanticSearch = true,
            UsedToolCalling = true,
            ResultCount = 4
        };
        context.CopilotSessions.Add(copilotSession);
        await context.SaveChangesAsync();
        context.CopilotResultFeedbacks.Add(new CopilotResultFeedback
        {
            CopilotSessionId = copilotSession.Id,
            DogId = 1,
            AdopterUserId = TestDbContextFactory.AdopterId,
            FeedbackType = CopilotFeedbackType.Positive,
            CreatedAt = CreatedAt(8)
        });
        await context.SaveChangesAsync();
        var service = new AnalyticsService(context);

        var dashboard = await service.GetAdminAnalyticsAsync(JanuaryRange(), null, TestDbContextFactory.AdminId);

        Assert.Equal(1, dashboard.ResourceAnalytics.LowStockResources);
        Assert.Equal(2, dashboard.ReportActivity.ReportsGenerated);
        Assert.Equal(1, dashboard.ReportActivity.FailedReports);
        Assert.NotNull(dashboard.CopilotAnalytics);
        Assert.Equal(1, dashboard.CopilotAnalytics!.Sessions);
        Assert.Equal(1, dashboard.CopilotAnalytics.PositiveFeedback);
        Assert.DoesNotContain("raw adopter prompt", dashboard.CopilotAnalytics.TopIntents[0].Intent);
    }

    private static AnalyticsDateRange JanuaryRange()
    {
        return new AnalyticsDateRange(CreatedAt(1), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), "January 2026");
    }

    private static DateTime CreatedAt(int day)
    {
        return new DateTime(2026, 1, day, 10, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime UpdatedAt(int day)
    {
        return new DateTime(2026, 1, day, 12, 0, 0, DateTimeKind.Utc);
    }

    private static AdoptionRequest Request(
        int dogId,
        AdoptionRequestStatus status,
        DateTime createdAt,
        DateTime? updatedAt = null,
        string adopterId = TestDbContextFactory.AdopterId)
    {
        return new AdoptionRequest
        {
            DogId = dogId,
            AdopterId = adopterId,
            Status = status,
            VisitStatus = status == AdoptionRequestStatus.VisitConfirmed ? AdoptionVisitStatus.Confirmed : AdoptionVisitStatus.Requested,
            ReasonForAdoption = "A realistic reason for adoption.",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt ?? createdAt
        };
    }

    private static ResourceStock Resource(
        string name,
        int shelterId,
        int quantity,
        int threshold,
        int categoryId = TestDbContextFactory.FoodCategoryId)
    {
        return new ResourceStock
        {
            Name = name,
            ShelterId = shelterId,
            ResourceCategoryId = categoryId,
            Quantity = quantity,
            LowStockThreshold = threshold,
            Unit = "kg"
        };
    }
}
