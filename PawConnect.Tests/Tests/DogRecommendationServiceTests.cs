using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var available = TestDbContextFactory.CreateDog("Available Match", DogStatus.Available);
        var adopted = TestDbContextFactory.CreateDog("Adopted Hidden", DogStatus.Adopted);
        var treatment = TestDbContextFactory.CreateDog("Treatment Hidden", DogStatus.InTreatment);
        context.Dogs.AddRange(available, adopted, treatment);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var recommendations = await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId);

        Assert.Contains(recommendations, recommendation => recommendation.Dog.Name == "Available Match");
        Assert.DoesNotContain(recommendations, recommendation => recommendation.Dog.Name == "Adopted Hidden");
        Assert.DoesNotContain(recommendations, recommendation => recommendation.Dog.Name == "Treatment Hidden");
    }

    [Fact]
    public async Task RuleBasedRecommendations_SameCityDogScoresHigher()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Bucharest");
        var sameCity = TestDbContextFactory.CreateDog("Same City", shelterId: TestDbContextFactory.ShelterId);
        var otherCity = TestDbContextFactory.CreateDog("Other City", shelterId: TestDbContextFactory.OtherShelterId);
        context.Dogs.AddRange(sameCity, otherCity);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var recommendations = await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId);

        Assert.Equal("Same City", recommendations.First().Dog.Name);
        Assert.True(recommendations.First(r => r.Dog.Name == "Same City").Score >
                    recommendations.First(r => r.Dog.Name == "Other City").Score);
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

        Assert.True(recommendations.First(r => r.Dog.Name == "Apartment Small").Score >
                    recommendations.First(r => r.Dog.Name == "Apartment Large").Score);
    }

    [Fact]
    public async Task RuleBasedRecommendations_HouseOrYardProfileCanFavorLargerDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Iasi", housingType: HousingType.House, hasYard: true);
        var largeDog = TestDbContextFactory.CreateDog("Yard Large");
        largeDog.Size = DogSize.Large;
        var smallDog = TestDbContextFactory.CreateDog("Yard Small");
        smallDog.Size = DogSize.Small;
        context.Dogs.AddRange(largeDog, smallDog);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var recommendations = await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId);

        Assert.True(recommendations.First(r => r.Dog.Name == "Yard Large").Score >
                    recommendations.First(r => r.Dog.Name == "Yard Small").Score);
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
    public async Task RuleBasedRecommendations_GenerateReadableReasons()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Bucharest", hasChildren: true);
        var dog = TestDbContextFactory.CreateDog("Reason Dog");
        dog.BehaviorDescription = "Gentle family dog that is calm around children.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var recommendation = (await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId)).Single();

        Assert.NotEmpty(recommendation.Reasons);
        Assert.Contains(recommendation.Reasons, reason => reason.Contains("Same city", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RuleBasedRecommendations_CalculateNormalizedMatchScoreAndLabel()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Bucharest", hasChildren: true);
        var dog = TestDbContextFactory.CreateDog("Scored Dog");
        dog.Size = DogSize.Small;
        dog.BehaviorDescription = "Gentle family dog that is calm and social.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var recommendation = (await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId)).Single();

        Assert.InRange(recommendation.MatchPercentage, 52, 96);
        Assert.Equal("Excellent match", recommendation.MatchLevel);
        Assert.NotEqual(0, recommendation.MatchPercentage);
    }

    [Fact]
    public async Task RuleBasedRecommendations_CategorizeReasons()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Bucharest", hasChildren: true);
        var dog = TestDbContextFactory.CreateDog("Categorized Dog");
        dog.BehaviorDescription = "Gentle family dog that is social with other dogs.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var recommendation = (await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId)).Single();

        Assert.NotNull(recommendation.ReasonCategories);
        Assert.Contains(recommendation.ReasonCategories!, reason => reason.Category == "Location fit");
        Assert.Contains(recommendation.ReasonCategories!, reason => reason.Category == "Behavior fit");
    }

    [Fact]
    public async Task RuleBasedRecommendations_FavoritesAndRecentViewsInfluenceButDoNotDominate()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Bucharest");
        var savedDog = TestDbContextFactory.CreateDog("Saved Trait Dog", DogStatus.Adopted);
        savedDog.Breed = "Rare Breed";
        var sameCityDog = TestDbContextFactory.CreateDog("Same City Candidate", shelterId: TestDbContextFactory.ShelterId);
        sameCityDog.Breed = "Different Breed";
        var preferenceDog = TestDbContextFactory.CreateDog("Preference Candidate", shelterId: TestDbContextFactory.OtherShelterId);
        preferenceDog.Breed = "Rare Breed";
        context.Dogs.AddRange(savedDog, sameCityDog, preferenceDog);
        await context.SaveChangesAsync();
        context.FavoriteDogs.Add(new FavoriteDog
        {
            AdopterId = TestDbContextFactory.AdopterId,
            DogId = savedDog.Id
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var recommendations = await service.GetRuleBasedRecommendationsAsync(TestDbContextFactory.AdopterId);

        Assert.True(recommendations.First(r => r.Dog.Name == "Same City Candidate").Score >
                    recommendations.First(r => r.Dog.Name == "Preference Candidate").Score);
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
    public async Task Recommendations_MissingOpenAiApiKeyUsesRuleBasedResults()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        context.Dogs.Add(TestDbContextFactory.CreateDog("No Key Dog"));
        await context.SaveChangesAsync();
        var openAiClient = new FakeOpenAiRecommendationClient();
        var service = CreateService(context, new OpenAiSettings { Enabled = true, ApiKey = "" }, openAiClient);

        var recommendations = await service.GetRecommendationsForAdopterAsync(TestDbContextFactory.AdopterId);

        Assert.Single(recommendations);
        Assert.False(recommendations[0].UsedAiEnhancement);
        Assert.False(openAiClient.WasCalled);
    }

    [Fact]
    public async Task Recommendations_OpenAiFailureFallsBackToRuleBasedResults()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        context.Dogs.Add(TestDbContextFactory.CreateDog("Fallback Dog"));
        await context.SaveChangesAsync();
        var openAiClient = new FakeOpenAiRecommendationClient
        {
            Response = OpenAiRecommendationResponse.Failed("fake failure")
        };
        var service = CreateService(context, EnabledOpenAiSettings(), openAiClient);

        var recommendations = await service.GetRecommendationsForAdopterAsync(TestDbContextFactory.AdopterId);

        Assert.Single(recommendations);
        Assert.Equal("Fallback Dog", recommendations[0].Dog.Name);
        Assert.False(recommendations[0].UsedAiEnhancement);
        Assert.True(openAiClient.WasCalled);
    }

    [Fact]
    public async Task Recommendations_OpenAiResponseCanReorderKnownCandidates()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Iasi");
        var firstDog = TestDbContextFactory.CreateDog("Rule First");
        firstDog.Size = DogSize.Small;
        var secondDog = TestDbContextFactory.CreateDog("Ai First");
        secondDog.Size = DogSize.Large;
        context.Dogs.AddRange(firstDog, secondDog);
        await context.SaveChangesAsync();
        var openAiClient = new FakeOpenAiRecommendationClient
        {
            ResponseFactory = request => new OpenAiRecommendationResponse(true,
            [
                new OpenAiRecommendationItem(
                    request.Candidates.Single(c => c.Breed == "Mixed Breed" && c.DogId != firstDog.Id).DogId,
                    1,
                    "Good match",
                    ["Concise adopter-friendly reason"],
                    "A concise improved summary.",
                    [new OpenAiRecommendationReason("Home fit", "AI home-fit reason")]),
                new OpenAiRecommendationItem(firstDog.Id, 2, "Possible match", ["Another reason"])
            ])
        };
        var service = CreateService(context, EnabledOpenAiSettings(), openAiClient);

        var recommendations = await service.GetRecommendationsForAdopterAsync(TestDbContextFactory.AdopterId, 2);

        Assert.Equal(secondDog.Id, recommendations[0].DogId);
        Assert.True(recommendations[0].UsedAiEnhancement);
        Assert.Contains("Concise adopter-friendly reason", recommendations[0].Reasons);
        Assert.Equal("A concise improved summary.", recommendations[0].ShortSummary);
        Assert.Contains(recommendations[0].ReasonCategories!, reason => reason.Category == "Home fit" && reason.Text == "AI home-fit reason");
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
        PawConnect.Data.ApplicationDbContext context,
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
        PawConnect.Data.ApplicationDbContext context,
        string city = "Bucharest",
        HousingType housingType = HousingType.Apartment,
        bool hasYard = false,
        bool hasChildren = false,
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
            HasChildren = hasChildren,
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

        public Func<RecommendationOpenAiRequest, OpenAiRecommendationResponse>? ResponseFactory { get; set; }

        public Task<OpenAiRecommendationResponse> GetEnhancedRecommendationsAsync(
            RecommendationOpenAiRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastRequest = request;
            return Task.FromResult(ResponseFactory?.Invoke(request) ?? Response);
        }
    }
}
