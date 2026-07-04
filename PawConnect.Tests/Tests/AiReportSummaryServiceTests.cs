using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class AiReportSummaryServiceTests
{
    [Fact]
    public async Task GenerateShelterSummaryAsync_WhenOpenAiDisabled_ReturnsDeterministicFallback()
    {
        var service = CreateService(settings: new OpenAiSettings
        {
            Enabled = false,
            ApiKey = string.Empty,
            ReportSummariesEnabled = true
        });

        var result = await service.GenerateShelterSummaryAsync(CreateMetrics());

        Assert.False(result.UsedAi);
        Assert.Contains("3 dogs", result.ExecutiveSummary);
        Assert.Contains("2 available", result.ExecutiveSummary);
        Assert.Contains("1 new adoption request", result.ExecutiveSummary);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public async Task GenerateShelterSummaryAsync_UsesValidOpenAiSummary()
    {
        var fakeClient = new FakeOpenAiReportSummaryClient
        {
            Response = OpenAiReportSummaryResponse.Successful(new AiReportSummaryResult(
                "Shelter Report Summary",
                "Happy Paws had 3 dogs, 2 available dogs, and 1 low-stock resource.",
                ["Adoption activity is visible in the report."],
                ["Inventory needs attention."],
                ["Review the low-stock resource."],
                ["This is based only on report metrics."],
                UsedAi: true,
                FallbackReason: null))
        };
        var service = CreateService(fakeClient);

        var result = await service.GenerateShelterSummaryAsync(CreateMetrics());

        Assert.True(result.UsedAi);
        Assert.Null(result.FallbackReason);
        Assert.Equal("Happy Paws had 3 dogs, 2 available dogs, and 1 low-stock resource.", result.ExecutiveSummary);
    }

    [Fact]
    public async Task GenerateShelterSummaryAsync_RejectsUnsupportedNumbersFromOpenAi()
    {
        var fakeClient = new FakeOpenAiReportSummaryClient
        {
            Response = OpenAiReportSummaryResponse.Successful(new AiReportSummaryResult(
                "Shelter Report Summary",
                "The shelter had 999 adoptions this month.",
                [],
                [],
                [],
                [],
                UsedAi: true,
                FallbackReason: null))
        };
        var service = CreateService(fakeClient);

        var result = await service.GenerateShelterSummaryAsync(CreateMetrics());

        Assert.False(result.UsedAi);
        Assert.Contains("unsupported numbers", result.FallbackReason);
    }

    [Fact]
    public async Task GenerateShelterSummaryAsync_WhenClientFails_ReturnsFallback()
    {
        var fakeClient = new FakeOpenAiReportSummaryClient { ThrowOnGenerate = true };
        var service = CreateService(fakeClient);

        var result = await service.GenerateShelterSummaryAsync(CreateMetrics());

        Assert.False(result.UsedAi);
        Assert.Contains("failed", result.FallbackReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShelterReportSummaryMetricsDto_DoesNotExposePrivateContactOrSecretFields()
    {
        var propertyNames = typeof(ShelterReportSummaryMetricsDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, name => name.Contains("Email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Phone", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Adopter", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Message", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PdfShelterSummaryReport_UsesShelterScopedMetricsForNarrativeSummary()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var fromDate = DateTime.UtcNow.AddDays(-1);
        var toDate = DateTime.UtcNow;
        var shelterDog = TestDbContextFactory.CreateDog("Shelter Dog", DogStatus.Available);
        var reservedDog = TestDbContextFactory.CreateDog("Reserved Dog", DogStatus.Reserved);
        var otherShelterDog = TestDbContextFactory.CreateDog("Other Dog", DogStatus.Available, TestDbContextFactory.OtherShelterId);
        context.Dogs.AddRange(shelterDog, reservedDog, otherShelterDog);
        context.SaveChanges();

        context.AdoptionRequests.AddRange(
            new AdoptionRequest
            {
                DogId = shelterDog.Id,
                AdopterId = TestDbContextFactory.AdopterId,
                Status = AdoptionRequestStatus.Pending,
                ReasonForAdoption = "I can offer a calm home.",
                CreatedAt = toDate.AddHours(-3),
                UpdatedAt = toDate.AddHours(-3)
            },
            new AdoptionRequest
            {
                DogId = otherShelterDog.Id,
                AdopterId = TestDbContextFactory.SecondAdopterId,
                Status = AdoptionRequestStatus.Pending,
                ReasonForAdoption = "Different shelter request.",
                CreatedAt = toDate.AddHours(-2),
                UpdatedAt = toDate.AddHours(-2)
            });
        context.ResourceStocks.AddRange(
            new ResourceStock
            {
                ShelterId = TestDbContextFactory.ShelterId,
                ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
                Name = "Adult dry food",
                Quantity = 2,
                LowStockThreshold = 5,
                Unit = "bags"
            },
            new ResourceStock
            {
                ShelterId = TestDbContextFactory.OtherShelterId,
                ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
                Name = "Other shelter food",
                Quantity = 1,
                LowStockThreshold = 5,
                Unit = "bags"
            });
        context.SaveChanges();

        var summaryService = new CapturingAiReportSummaryService();
        var pdfService = new PdfReportService(
            context,
            NullLogger<PdfReportService>.Instance,
            summaryService);

        var pdf = await pdfService.GenerateShelterSummaryReportAsync(TestDbContextFactory.ShelterId, fromDate, toDate);

        Assert.NotEmpty(pdf);
        Assert.NotNull(summaryService.LastMetrics);
        Assert.Equal(2, summaryService.LastMetrics.TotalDogs);
        Assert.Equal(1, summaryService.LastMetrics.NewRequestsInPeriod);
        Assert.Equal(1, summaryService.LastMetrics.LowStockResourceCount);
        Assert.Equal("Adult dry food", Assert.Single(summaryService.LastMetrics.CriticalLowStockResources).Name);
    }

    private static AiReportSummaryService CreateService(
        FakeOpenAiReportSummaryClient? fakeClient = null,
        OpenAiSettings? settings = null)
    {
        return new AiReportSummaryService(
            fakeClient ?? new FakeOpenAiReportSummaryClient(),
            Options.Create(settings ?? EnabledSettings()),
            NullLogger<AiReportSummaryService>.Instance);
    }

    private static OpenAiSettings EnabledSettings()
    {
        return new OpenAiSettings
        {
            Enabled = true,
            ApiKey = "test-key",
            ReportSummariesEnabled = true
        };
    }

    private static ShelterReportSummaryMetricsDto CreateMetrics()
    {
        return new ShelterReportSummaryMetricsDto(
            "Happy Paws",
            "Cluj-Napoca",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            TotalDogs: 3,
            AvailableDogs: 2,
            ReservedDogs: 0,
            AdoptedDogs: 1,
            InTreatmentDogs: 0,
            NewRequestsInPeriod: 1,
            PendingRequests: 1,
            ConfirmedVisitsInPeriod: 0,
            AcceptedRequests: 0,
            RejectedRequests: 0,
            CancelledRequests: 0,
            TotalRequests: 1,
            RecentlyAdoptedDogs: 1,
            LowStockResourceCount: 1,
            CriticalLowStockResources:
            [
                new LowStockResourceSummaryDto("Adult dry food", "Food", 2, 5, "bags")
            ],
            AverageDecisionDays: null);
    }

    private sealed class FakeOpenAiReportSummaryClient : IOpenAiReportSummaryClient
    {
        public OpenAiReportSummaryResponse Response { get; set; } =
            OpenAiReportSummaryResponse.Failed("Not configured in test.");

        public bool ThrowOnGenerate { get; set; }

        public Task<OpenAiReportSummaryResponse> GenerateSummaryAsync(
            AiReportSummaryRequest request,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnGenerate)
            {
                throw new HttpRequestException("OpenAI unavailable.");
            }

            return Task.FromResult(Response);
        }
    }

    private sealed class CapturingAiReportSummaryService : IAiReportSummaryService
    {
        public ShelterReportSummaryMetricsDto? LastMetrics { get; private set; }

        public Task<AiReportSummaryResult> GenerateShelterSummaryAsync(
            ShelterReportSummaryMetricsDto metrics,
            CancellationToken cancellationToken = default)
        {
            LastMetrics = metrics;
            return Task.FromResult(AiReportSummaryService.BuildShelterFallback(metrics, null));
        }
    }
}
