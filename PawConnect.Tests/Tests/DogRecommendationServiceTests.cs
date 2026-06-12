using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class DogRecommendationServiceTests
{
    [Fact]
    public async Task RuleBasedRecommendations_ExcludeAdoptedAndInTreatmentDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Available Match", DogStatus.Available),
            TestDbContextFactory.CreateDog("Reserved Match", DogStatus.Reserved),
            TestDbContextFactory.CreateDog("Adopted Hidden", DogStatus.Adopted),
            TestDbContextFactory.CreateDog("Treatment Hidden", DogStatus.InTreatment));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var recommendations = await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId);

        Assert.Contains(recommendations, recommendation => recommendation.Dog.Name == "Available Match");
        Assert.Contains(recommendations, recommendation => recommendation.Dog.Name == "Reserved Match");
        Assert.DoesNotContain(recommendations, recommendation => recommendation.Dog.Name == "Adopted Hidden");
        Assert.DoesNotContain(recommendations, recommendation => recommendation.Dog.Name == "Treatment Hidden");
    }

    [Fact]
    public async Task RuleBasedRecommendations_ApartmentProfileFavorsSmallOrMediumDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Iasi", housingType: HousingType.Apartment);
        var smallDog = TestDbContextFactory.CreateDog("Apartment Small");
        smallDog.Size = DogSize.Small;
        var largeDog = TestDbContextFactory.CreateDog("Apartment Large");
        largeDog.Size = DogSize.Large;
        context.Dogs.AddRange(smallDog, largeDog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var recommendations = await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId);

        Assert.True(recommendations.First(item => item.Dog.Name == "Apartment Small").Score >
                    recommendations.First(item => item.Dog.Name == "Apartment Large").Score);
    }

    [Fact]
    public async Task RuleBasedRecommendations_MissingAdopterProfileReturnsEmptyResult()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.Add(TestDbContextFactory.CreateDog("Visible Dog"));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var recommendations = await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId);

        Assert.Empty(recommendations);
    }

    [Fact]
    public async Task Recommendations_OpenAiDisabledUsesRuleBasedResults()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        context.Dogs.Add(TestDbContextFactory.CreateDog("Rule Dog"));
        await context.SaveChangesAsync();
        var openAiClient = new FakeOpenAiRecommendationClient();
        var service = CreateService(context, new OpenAiSettings { Enabled = false }, openAiClient);

        var recommendations = await service.GetRecommendationsForAdopterAsync(TestDbContextFactory.AdopterId);

        Assert.Single(recommendations);
        Assert.False(recommendations[0].UsedAiEnhancement);
        Assert.False(openAiClient.WasCalled);
    }

    [Fact]
    public async Task Recommendations_IgnoreUnknownOpenAiDogIdsAndCannotAddUnavailableDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var available = TestDbContextFactory.CreateDog("Allowed Dog", DogStatus.Available);
        var adopted = TestDbContextFactory.CreateDog("Adopted Dog", DogStatus.Adopted);
        context.Dogs.AddRange(available, adopted);
        await context.SaveChangesAsync();
        var openAiClient = new FakeOpenAiRecommendationClient
        {
            Response = new OpenAiRecommendationResponse(true,
            [
                new OpenAiRecommendationItem(99999, 1, "Excellent match", ["Unknown dog should be ignored"]),
                new OpenAiRecommendationItem(adopted.Id, 2, "Excellent match", ["Unavailable dog should be ignored"]),
                new OpenAiRecommendationItem(available.Id, 3, "Good match", ["Known dog remains"])
            ])
        };
        var service = CreateService(context, EnabledOpenAiSettings(), openAiClient);

        var recommendations = await service.GetRecommendationsForAdopterAsync(TestDbContextFactory.AdopterId);

        Assert.Single(recommendations);
        Assert.Equal(available.Id, recommendations[0].DogId);
    }

    [Fact]
    public async Task Recommendations_OpenAiRequestDoesNotIncludeSensitiveAdopterFields()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(
            context,
            fullName: "Sensitive Full Name",
            address: "Secret Street 7",
            phoneNumber: "0700000000",
            additionalNotes: "Do not send this note");
        context.Dogs.Add(TestDbContextFactory.CreateDog("Privacy Dog"));
        await context.SaveChangesAsync();
        var openAiClient = new FakeOpenAiRecommendationClient
        {
            Response = new OpenAiRecommendationResponse(true, [])
        };
        var service = CreateService(context, EnabledOpenAiSettings(), openAiClient);

        await service.GetRecommendationsForAdopterAsync(TestDbContextFactory.AdopterId);

        var requestJson = JsonSerializer.Serialize(openAiClient.LastRequest);
        Assert.DoesNotContain("Sensitive Full Name", requestJson);
        Assert.DoesNotContain("Secret Street 7", requestJson);
        Assert.DoesNotContain("0700000000", requestJson);
        Assert.DoesNotContain("Do not send this note", requestJson);
        Assert.DoesNotContain("Email", requestJson);
        Assert.DoesNotContain("PhoneNumber", requestJson);
        Assert.DoesNotContain("FullName", requestJson);
        Assert.DoesNotContain("Address", requestJson);
        Assert.DoesNotContain("AdditionalNotes", requestJson);
    }

    private static DogRecommendationService CreateService(
        ApplicationDbContext context,
        OpenAiSettings? settings = null,
        FakeOpenAiRecommendationClient? openAiClient = null)
    {
        return new DogRecommendationService(
            context,
            Options.Create(settings ?? new OpenAiSettings()),
            openAiClient ?? new FakeOpenAiRecommendationClient(),
            NullLogger<DogRecommendationService>.Instance);
    }

    private static OpenAiSettings EnabledOpenAiSettings()
    {
        return new OpenAiSettings
        {
            Enabled = true,
            ApiKey = "test-key",
            Model = "gpt-5.4-mini"
        };
    }

    private static void SeedProfile(
        ApplicationDbContext context,
        string city = "Bucharest",
        HousingType housingType = HousingType.Apartment,
        bool hasYard = false,
        string fullName = "Test Adopter",
        string? address = null,
        string? phoneNumber = null,
        string? additionalNotes = null)
    {
        context.AdopterProfiles.Add(new AdopterProfile
        {
            ApplicationUserId = TestDbContextFactory.AdopterId,
            FullName = fullName,
            City = city,
            Address = address,
            PhoneNumber = phoneNumber,
            HousingType = housingType,
            HasYard = hasYard,
            HasChildren = false,
            HasOtherPets = true,
            ExperienceWithDogs = "Comfortable with active dogs and training.",
            AdditionalNotes = additionalNotes
        });

        context.SaveChanges();
    }

    private sealed class FakeOpenAiRecommendationClient : IOpenAiRecommendationClient
    {
        public bool WasCalled { get; private set; }

        public RecommendationOpenAiRequest? LastRequest { get; private set; }

        public OpenAiRecommendationResponse Response { get; set; } = OpenAiRecommendationResponse.Failed("not configured");

        public Task<OpenAiRecommendationResponse> GetEnhancedRecommendationsAsync(
            RecommendationOpenAiRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }
}
