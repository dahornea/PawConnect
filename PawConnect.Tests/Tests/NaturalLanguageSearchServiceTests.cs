using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class NaturalLanguageSearchServiceTests
{
    [Fact]
    public async Task SearchAdminAsync_PendingRequestsQueryReturnsOnlyPendingRequests()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Bella");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.AddRange(
            Request(dog.Id, AdoptionRequestStatus.Pending),
            Request(dog.Id, AdoptionRequestStatus.Rejected));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.SearchAdminAsync(new NaturalLanguageSearchRequest(
            "show pending requests",
            TestDbContextFactory.AdminId));

        Assert.Equal(NaturalLanguageSearchIntent.FindPendingRequests, result.Interpretation.Intent);
        Assert.Single(result.Items);
        Assert.Equal("Pending", result.Items[0].Status);
    }

    [Fact]
    public async Task SearchAdminAsync_LowStockQueryReturnsOnlyResourcesBelowThreshold()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.AddRange(
            Resource("Adult dry food", quantity: 2, threshold: 5),
            Resource("Blankets", quantity: 20, threshold: 5, categoryId: TestDbContextFactory.MedicineCategoryId));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.SearchAdminAsync(new NaturalLanguageSearchRequest(
            "show low stock resources",
            TestDbContextFactory.AdminId));

        Assert.Equal(NaturalLanguageSearchIntent.FindLowStockResources, result.Interpretation.Intent);
        Assert.Single(result.Items);
        Assert.Equal("Adult dry food", result.Items[0].Title);
        Assert.Equal("Low stock", result.Items[0].Status);
    }

    [Fact]
    public async Task SearchAdminAsync_BlocksNonAdminUsers()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SearchAdminAsync(new NaturalLanguageSearchRequest(
                "show pending requests",
                TestDbContextFactory.AdopterId)));

        Assert.Equal("Only administrators can use natural-language admin search.", exception.Message);
    }

    [Fact]
    public async Task SearchAdminAsync_OpenAiInterpretationIsValidatedBeforeExecution()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.Add(TestDbContextFactory.CreateDog("Available Dog", DogStatus.Available));
        await context.SaveChangesAsync();
        var fakeClient = new FakeOpenAiNaturalLanguageSearchClient
        {
            Response = OpenAiNaturalLanguageSearchResponse.Successful(new NaturalLanguageSearchInterpretation
            {
                Intent = NaturalLanguageSearchIntent.FindDogsByStatus,
                Scope = NaturalLanguageSearchScope.Dogs,
                DogStatus = DogStatus.Available,
                Limit = 500,
                SortField = "RawSql",
                Explanation = "AI mapped the query to available dogs."
            })
        };
        var service = CreateService(context, fakeClient, openAiEnabled: true);

        var result = await service.SearchAdminAsync(new NaturalLanguageSearchRequest(
            "find the operational dog list",
            TestDbContextFactory.AdminId));

        Assert.Single(result.Items);
        Assert.Equal(100, result.Interpretation.Limit);
        Assert.Null(result.Interpretation.SortField);
        Assert.Contains(result.Interpretation.Warnings, warning => warning.Contains("Unsupported sort field"));
    }

    [Fact]
    public async Task SearchAdminAsync_OpenAiRequestDoesNotIncludeDatabaseRows()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.Add(TestDbContextFactory.CreateDog("Private Dog Name", DogStatus.Available));
        await context.SaveChangesAsync();
        var fakeClient = new FakeOpenAiNaturalLanguageSearchClient
        {
            Response = OpenAiNaturalLanguageSearchResponse.Failed("Force fallback")
        };
        var service = CreateService(context, fakeClient, openAiEnabled: true);

        await service.SearchAdminAsync(new NaturalLanguageSearchRequest(
            "custom unsupported operational query",
            TestDbContextFactory.AdminId));

        Assert.NotNull(fakeClient.LastRequest);
        var serializedRequest = System.Text.Json.JsonSerializer.Serialize(fakeClient.LastRequest);
        Assert.DoesNotContain("Private Dog Name", serializedRequest);
        Assert.Contains("AllowedIntents", serializedRequest);
    }

    [Fact]
    public async Task SearchShelterAsync_PendingRequestsReturnsOnlyCurrentShelterData()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var ownDog = TestDbContextFactory.CreateDog("Own Dog", shelterId: TestDbContextFactory.ShelterId);
        var otherDog = TestDbContextFactory.CreateDog("Other Dog", shelterId: TestDbContextFactory.OtherShelterId);
        context.Dogs.AddRange(ownDog, otherDog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.AddRange(
            Request(ownDog.Id, AdoptionRequestStatus.Pending),
            Request(otherDog.Id, AdoptionRequestStatus.Pending, TestDbContextFactory.SecondAdopterId));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.SearchShelterAsync(new NaturalLanguageSearchRequest(
            "my pending requests",
            TestDbContextFactory.ShelterUserId));

        Assert.Single(result.Items);
        Assert.Equal("Request for Own Dog", result.Items[0].Title);
        Assert.Equal("/shelter/adoption-requests", result.Items[0].Link);
    }

    [Fact]
    public async Task SearchShelterAsync_AvailableDogsReturnsOnlyCurrentShelterDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Own Available", DogStatus.Available, TestDbContextFactory.ShelterId),
            TestDbContextFactory.CreateDog("Other Available", DogStatus.Available, TestDbContextFactory.OtherShelterId));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.SearchShelterAsync(new NaturalLanguageSearchRequest(
            "my available dogs",
            TestDbContextFactory.ShelterUserId));

        Assert.Single(result.Items);
        Assert.Equal("Own Available", result.Items[0].Title);
        Assert.StartsWith("/shelter/dogs/edit/", result.Items[0].Link);
    }

    [Fact]
    public async Task SearchShelterAsync_LowStockResourcesReturnsOnlyCurrentShelterResources()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.AddRange(
            Resource("Own Food", quantity: 2, threshold: 5, shelterId: TestDbContextFactory.ShelterId),
            Resource("Other Food", quantity: 1, threshold: 5, shelterId: TestDbContextFactory.OtherShelterId));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.SearchShelterAsync(new NaturalLanguageSearchRequest(
            "my low stock resources",
            TestDbContextFactory.ShelterUserId));

        Assert.Single(result.Items);
        Assert.Equal("Own Food", result.Items[0].Title);
        Assert.Equal("/shelter/resources", result.Items[0].Link);
    }

    [Fact]
    public async Task SearchShelterAsync_BlocksNonShelterUsers()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SearchShelterAsync(new NaturalLanguageSearchRequest(
                "my pending requests",
                TestDbContextFactory.AdopterId)));

        Assert.Equal("Only shelter accounts can use shelter operational search.", exception.Message);
    }

    [Fact]
    public async Task SearchShelterAsync_IgnoresOpenAiShelterNameFilter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Own Available", DogStatus.Available, TestDbContextFactory.ShelterId),
            TestDbContextFactory.CreateDog("Other Available", DogStatus.Available, TestDbContextFactory.OtherShelterId));
        await context.SaveChangesAsync();
        var fakeClient = new FakeOpenAiNaturalLanguageSearchClient
        {
            Response = OpenAiNaturalLanguageSearchResponse.Successful(new NaturalLanguageSearchInterpretation
            {
                Intent = NaturalLanguageSearchIntent.FindDogsByStatus,
                Scope = NaturalLanguageSearchScope.Dogs,
                DogStatus = DogStatus.Available,
                ShelterName = "Other Shelter",
                Explanation = "AI mapped the query to available dogs from another shelter."
            })
        };
        var service = CreateService(context, fakeClient, openAiEnabled: true);

        var result = await service.SearchShelterAsync(new NaturalLanguageSearchRequest(
            "custom shelter operational query",
            TestDbContextFactory.ShelterUserId));

        Assert.Single(result.Items);
        Assert.Equal("Own Available", result.Items[0].Title);
        Assert.Contains(result.Interpretation.Warnings, warning => warning.Contains("Shelter filters are ignored"));
    }

    private static NaturalLanguageSearchService CreateService(
        ApplicationDbContext context,
        FakeOpenAiNaturalLanguageSearchClient? openAiClient = null,
        bool openAiEnabled = false)
    {
        return new NaturalLanguageSearchService(
            context,
            TestDbContextFactory.CreateUserManager(context),
            openAiClient ?? new FakeOpenAiNaturalLanguageSearchClient(),
            Options.Create(new OpenAiSettings
            {
                Enabled = openAiEnabled,
                ApiKey = openAiEnabled ? "test-key" : string.Empty
            }),
            NullLogger<NaturalLanguageSearchService>.Instance);
    }

    private static AdoptionRequest Request(
        int dogId,
        AdoptionRequestStatus status,
        string adopterId = TestDbContextFactory.AdopterId)
    {
        return new AdoptionRequest
        {
            DogId = dogId,
            AdopterId = adopterId,
            Status = status,
            ReasonForAdoption = "A realistic reason for adoption.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ResourceStock Resource(
        string name,
        int quantity,
        int threshold,
        int categoryId = TestDbContextFactory.FoodCategoryId,
        int shelterId = TestDbContextFactory.ShelterId)
    {
        return new ResourceStock
        {
            Name = name,
            ShelterId = shelterId,
            ResourceCategoryId = categoryId,
            Quantity = quantity,
            Unit = "kg",
            LowStockThreshold = threshold
        };
    }

    private sealed class FakeOpenAiNaturalLanguageSearchClient : IOpenAiNaturalLanguageSearchClient
    {
        public NaturalLanguageSearchAiRequest? LastRequest { get; private set; }

        public OpenAiNaturalLanguageSearchResponse Response { get; set; } =
            OpenAiNaturalLanguageSearchResponse.Failed("OpenAI disabled for test.");

        public Task<OpenAiNaturalLanguageSearchResponse> InterpretAsync(
            NaturalLanguageSearchAiRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }
}
