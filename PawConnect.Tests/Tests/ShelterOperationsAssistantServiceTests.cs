using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class ShelterOperationsAssistantServiceTests
{
    [Fact]
    public async Task GenerateBriefAsync_BlocksNonShelterUsers()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateBriefAsync(TestDbContextFactory.AdopterId, new ShelterOperationsBriefRequest()));

        Assert.Equal("Only shelter accounts can use the shelter operations assistant.", exception.Message);
    }

    [Fact]
    public async Task GenerateBriefAsync_ReturnsOnlyCurrentShelterData()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var ownDog = TestDbContextFactory.CreateDog("Own Shelter Dog", shelterId: TestDbContextFactory.ShelterId);
        var otherDog = TestDbContextFactory.CreateDog("Other Shelter Dog", shelterId: TestDbContextFactory.OtherShelterId);
        context.Dogs.AddRange(ownDog, otherDog);
        await context.SaveChangesAsync();

        context.AdoptionRequests.AddRange(
            new AdoptionRequest
            {
                DogId = ownDog.Id,
                AdopterId = TestDbContextFactory.AdopterId,
                Status = AdoptionRequestStatus.Pending,
                VisitStatus = AdoptionVisitStatus.Requested,
                ReasonForAdoption = "I can offer a stable home.",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new AdoptionRequest
            {
                DogId = otherDog.Id,
                AdopterId = TestDbContextFactory.SecondAdopterId,
                Status = AdoptionRequestStatus.Pending,
                VisitStatus = AdoptionVisitStatus.Requested,
                ReasonForAdoption = "Different shelter data.",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            });
        context.ResourceStocks.AddRange(
            new ResourceStock
            {
                ShelterId = TestDbContextFactory.ShelterId,
                ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
                Name = "Own shelter food",
                Quantity = 1,
                LowStockThreshold = 4,
                Unit = "bags"
            },
            new ResourceStock
            {
                ShelterId = TestDbContextFactory.OtherShelterId,
                ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
                Name = "Other shelter food",
                Quantity = 1,
                LowStockThreshold = 4,
                Unit = "bags"
            });
        await context.SaveChangesAsync();

        var brief = await CreateService(context).GenerateBriefAsync(
            TestDbContextFactory.ShelterUserId,
            new ShelterOperationsBriefRequest(ShelterOperationsBriefPeriod.Next7Days));

        Assert.Contains(brief.RequestHighlights, request => request.DogName == "Own Shelter Dog");
        Assert.DoesNotContain(brief.RequestHighlights, request => request.DogName == "Other Shelter Dog");
        Assert.Contains(brief.LowStockItems, resource => resource.Name == "Own shelter food");
        Assert.DoesNotContain(brief.LowStockItems, resource => resource.Name == "Other shelter food");
    }

    [Fact]
    public async Task GenerateBriefAsync_AddsPriorityItemsForOldPendingRequestsAndLowStockResources()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Buddy", shelterId: TestDbContextFactory.ShelterId);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.Add(new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            Status = AdoptionRequestStatus.Pending,
            VisitStatus = AdoptionVisitStatus.NotScheduled,
            ReasonForAdoption = "I can offer a calm home.",
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow.AddDays(-7)
        });
        context.ResourceStocks.Add(new ResourceStock
        {
            ShelterId = TestDbContextFactory.ShelterId,
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            Name = "Adult dry food",
            Quantity = 0,
            LowStockThreshold = 5,
            Unit = "bags"
        });
        await context.SaveChangesAsync();

        var brief = await CreateService(context).GenerateBriefAsync(
            TestDbContextFactory.ShelterUserId,
            new ShelterOperationsBriefRequest(ShelterOperationsBriefPeriod.Next7Days));

        Assert.Contains(brief.PriorityItems, item => item.Category == ShelterOperationsCategory.AdoptionRequest);
        Assert.Contains(brief.PriorityItems, item =>
            item.Category == ShelterOperationsCategory.ResourceStock &&
            item.Priority == ShelterOperationsPriority.Critical);
    }

    [Fact]
    public async Task GenerateBriefAsync_WhenOpenAiDisabled_UsesFallbackAndDoesNotCallClient()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var fakeClient = new FakeOpenAiShelterOperationsAssistantClient();
        var service = CreateService(context, fakeClient, new OpenAiSettings
        {
            Enabled = false,
            ApiKey = string.Empty,
            ShelterOperationsAssistantEnabled = true
        });

        var brief = await service.GenerateBriefAsync(
            TestDbContextFactory.ShelterUserId,
            new ShelterOperationsBriefRequest());

        Assert.False(brief.UsedAi);
        Assert.NotNull(brief.FallbackReason);
        Assert.False(fakeClient.WasCalled);
    }

    [Fact]
    public async Task GenerateBriefAsync_OpenAiCanRephraseButReceivesSanitizedInput()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.Add(new ResourceStock
        {
            ShelterId = TestDbContextFactory.ShelterId,
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            Name = "Adult dry food",
            Quantity = 1,
            LowStockThreshold = 5,
            Unit = "bags"
        });
        await context.SaveChangesAsync();
        var fakeClient = new FakeOpenAiShelterOperationsAssistantClient
        {
            Response = OpenAiShelterOperationsAssistantResponse.Successful(new ShelterOperationsAiBriefDto(
                "Focus first on the resource item that is under threshold.",
                [
                    new ShelterOperationsAiPriorityItemDto(
                        "Critical",
                        "ResourceStock",
                        "Adult dry food is below threshold",
                        "Adult dry food is critically low; check the stock before handling lower-priority tasks.",
                        "Review resources")
                ],
                [],
                [],
                []))
        };
        var service = CreateService(context, fakeClient, new OpenAiSettings
        {
            Enabled = true,
            ApiKey = "test-key",
            ShelterOperationsAssistantEnabled = true
        });

        var brief = await service.GenerateBriefAsync(
            TestDbContextFactory.ShelterUserId,
            new ShelterOperationsBriefRequest());

        Assert.True(brief.UsedAi);
        Assert.Equal("Focus first on the resource item that is under threshold.", brief.ExecutiveSummary);
        var priority = Assert.Single(brief.PriorityItems, item => item.Category == ShelterOperationsCategory.ResourceStock);
        Assert.True(priority.IsAiGeneratedText);
        Assert.Equal("/shelter/resources", priority.ActionLink);
        Assert.NotNull(priority.RelatedEntityId);
        Assert.NotNull(fakeClient.LastInput);
        Assert.All(fakeClient.LastInput!.PriorityItems, item =>
        {
            Assert.Null(item.RelatedEntityId);
            Assert.Null(item.ActionLink);
            Assert.Null(item.ActionLabel);
        });
        var serializedInput = JsonSerializer.Serialize(fakeClient.LastInput);
        Assert.DoesNotContain("adopter@test.com", serializedInput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Test Adopter", serializedInput, StringComparison.OrdinalIgnoreCase);
    }

    private static ShelterOperationsAssistantService CreateService(
        ApplicationDbContext context,
        IOpenAiShelterOperationsAssistantClient? openAiClient = null,
        OpenAiSettings? settings = null)
    {
        return new ShelterOperationsAssistantService(
            context,
            openAiClient ?? new FakeOpenAiShelterOperationsAssistantClient(),
            Options.Create(settings ?? DisabledSettings()),
            NullLogger<ShelterOperationsAssistantService>.Instance);
    }

    private static OpenAiSettings DisabledSettings()
    {
        return new OpenAiSettings
        {
            Enabled = false,
            ApiKey = string.Empty,
            ShelterOperationsAssistantEnabled = true
        };
    }

    private sealed class FakeOpenAiShelterOperationsAssistantClient : IOpenAiShelterOperationsAssistantClient
    {
        public bool WasCalled { get; private set; }

        public ShelterOperationsBriefInputDto? LastInput { get; private set; }

        public OpenAiShelterOperationsAssistantResponse Response { get; init; } =
            OpenAiShelterOperationsAssistantResponse.Failed("OpenAI disabled for test.");

        public Task<OpenAiShelterOperationsAssistantResponse> GenerateBriefAsync(
            ShelterOperationsBriefInputDto input,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastInput = input;
            return Task.FromResult(Response);
        }
    }
}
