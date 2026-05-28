using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class SemanticDogSearchServiceTests
{
    [Fact]
    public void DogSearchDocument_IncludesPublicFieldsAndExcludesSensitiveShelterContactFields()
    {
        var dog = TestDbContextFactory.CreateDog("Max");
        dog.Breed = "Labrador Retriever \u00d7 Border Collie Mix";
        dog.DogBreed = DogBreedSeedData.CreateSeedEntities().First(breed => breed.Name == "Labrador Retriever");
        dog.SecondaryBreed = DogBreedSeedData.CreateSeedEntities().First(breed => breed.Name == "Border Collie");
        dog.IsMixedBreed = true;
        dog.BehaviorDescription = "Friendly, calm, and social.";
        dog.Shelter = new Shelter
        {
            Name = "Happy Paws",
            Neighborhood = "Zorilor",
            City = "Cluj-Napoca",
            Email = "private-shelter@example.com",
            PhoneNumber = "0700000000"
        };
        var service = new DogSearchDocumentService();

        var document = service.BuildDocument(dog);

        Assert.Contains("Max", document);
        Assert.Contains("Labrador Retriever \u00d7 Border Collie Mix", document);
        Assert.Contains("Border Collie", document);
        Assert.Contains("Friendly, calm, and social.", document);
        Assert.Contains("Happy Paws", document);
        Assert.Contains("Zorilor", document);
        Assert.Contains("Cluj-Napoca", document);
        Assert.DoesNotContain("private-shelter@example.com", document);
        Assert.DoesNotContain("0700000000", document);
    }

    [Fact]
    public async Task EmbeddingRefresh_DoesNotRegenerateWhenContentHashIsUnchanged()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Calm Dog");
        dog.Description = "A calm apartment companion.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var embeddingService = new FakeEmbeddingService();
        var service = new DogSearchEmbeddingService(
            context,
            new DogSearchDocumentService(),
            embeddingService,
            Options.Create(EnabledOpenAiSettings()),
            NullLogger<DogSearchEmbeddingService>.Instance);

        var firstRefresh = await service.RefreshDogEmbeddingAsync(dog.Id);
        var secondRefresh = await service.RefreshDogEmbeddingAsync(dog.Id);

        Assert.True(firstRefresh);
        Assert.False(secondRefresh);
        Assert.Equal(1, embeddingService.GenerateCallCount);
    }

    [Fact]
    public async Task RebuildIndex_CreatesEmbeddingsForAvailableAndReservedDogsOnly()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Available Indexed", DogStatus.Available),
            TestDbContextFactory.CreateDog("Reserved Indexed", DogStatus.Reserved),
            TestDbContextFactory.CreateDog("Adopted Hidden", DogStatus.Adopted),
            TestDbContextFactory.CreateDog("Treatment Hidden", DogStatus.InTreatment));
        await context.SaveChangesAsync();
        var service = CreateEmbeddingIndex(context, new FakeEmbeddingService());

        var result = await service.RebuildDogSearchIndexAsync();

        Assert.True(result.IsConfigured);
        Assert.Equal(2, result.SearchableDogCount);
        Assert.Equal(2, result.Created);
        Assert.Equal(2, context.DogSearchEmbeddings.Count());
        Assert.All(context.DogSearchEmbeddings.Include(e => e.Dog), embedding =>
            Assert.True(embedding.Dog!.Status is DogStatus.Available or DogStatus.Reserved));
    }

    [Fact]
    public async Task RebuildIndex_RemovesStaleEmbeddingsForNoLongerSearchableDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Stale Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateEmbeddingIndex(context, new FakeEmbeddingService());
        await service.RebuildDogSearchIndexAsync();
        dog.Status = DogStatus.Adopted;
        await context.SaveChangesAsync();

        var result = await service.RebuildDogSearchIndexAsync();

        Assert.Equal(1, result.Removed);
        Assert.Empty(context.DogSearchEmbeddings);
    }

    [Fact]
    public async Task RebuildIndex_MissingApiKeyReturnsSafeFailureWithoutCallingOpenAi()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.Add(TestDbContextFactory.CreateDog("No Key Dog", DogStatus.Available));
        await context.SaveChangesAsync();
        var embeddingService = new FakeEmbeddingService();
        var service = CreateEmbeddingIndex(
            context,
            embeddingService,
            new OpenAiSettings { Enabled = true, ApiKey = "", EmbeddingModel = "text-embedding-3-small" });

        var result = await service.RebuildDogSearchIndexAsync();

        Assert.False(result.IsConfigured);
        Assert.True(result.OpenAiEnabled);
        Assert.False(result.HasApiKey);
        Assert.Equal(0, embeddingService.GenerateCallCount);
        Assert.Empty(context.DogSearchEmbeddings);
    }

    [Fact]
    public void CosineSimilarity_RanksSimilarVectorsHigher()
    {
        var service = new FakeEmbeddingService();

        var sameDirection = service.CosineSimilarity([1, 0], [0.9f, 0.1f]);
        var differentDirection = service.CosineSimilarity([1, 0], [0, 1]);

        Assert.True(sameDirection > differentDirection);
        Assert.InRange(sameDirection, 0.9, 1.0);
    }

    [Fact]
    public async Task SemanticSearch_ExcludesAdoptedAndInTreatmentDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Available Calm", DogStatus.Available),
            TestDbContextFactory.CreateDog("Adopted Calm", DogStatus.Adopted),
            TestDbContextFactory.CreateDog("Treatment Calm", DogStatus.InTreatment));
        await context.SaveChangesAsync();
        var service = CreateSemanticService(context, settings: new OpenAiSettings { Enabled = false });

        var results = await service.SearchDogsAsync("calm dog", TestDbContextFactory.AdopterId);

        Assert.Contains(results, result => result.Dog.Name == "Available Calm");
        Assert.DoesNotContain(results, result => result.Dog.Name == "Adopted Calm");
        Assert.DoesNotContain(results, result => result.Dog.Name == "Treatment Calm");
    }

    [Fact]
    public async Task SemanticSearch_FallsBackWhenOpenAiIsDisabled()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Friendly Medium");
        dog.Description = "Friendly medium dog for a beginner.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var embeddingService = new FakeEmbeddingService();
        var service = CreateSemanticService(context, embeddingService, new OpenAiSettings { Enabled = false });

        var results = await service.SearchDogsAsync("friendly beginner", TestDbContextFactory.AdopterId);

        Assert.Single(results);
        Assert.Equal("Friendly Medium", results[0].Dog.Name);
        Assert.False(results[0].UsedSemanticEmbeddings);
        Assert.Equal(0, embeddingService.GenerateCallCount);
    }

    [Fact]
    public async Task SemanticSearch_FallsBackWhenEmbeddingClientFails()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Fallback Match");
        dog.Description = "Friendly beginner dog.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var embeddingService = new FakeEmbeddingService { FailGeneration = true };
        var service = CreateSemanticService(context, embeddingService, EnabledOpenAiSettings());

        var results = await service.SearchDogsAsync("friendly beginner", TestDbContextFactory.AdopterId);

        Assert.Single(results);
        Assert.Equal("Fallback Match", results[0].Dog.Name);
        Assert.False(results[0].UsedSemanticEmbeddings);
    }

    [Fact]
    public async Task SemanticSearch_FallsBackWhenEmbeddingsTableIsEmpty()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Empty Index Fallback");
        dog.Description = "Calm medium dog near Cluj.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var embeddingService = new FakeEmbeddingService();
        var service = CreateSemanticService(context, embeddingService, EnabledOpenAiSettings());

        var results = await service.SearchDogsAsync("calm medium dog", TestDbContextFactory.AdopterId);

        Assert.Single(results);
        Assert.Equal("Empty Index Fallback", results[0].Dog.Name);
        Assert.False(results[0].UsedSemanticEmbeddings);
    }

    [Fact]
    public async Task SemanticSearch_ProfileBonusAffectsRanking()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, city: "Bucharest", housingType: HousingType.Apartment);
        var sameCitySmall = TestDbContextFactory.CreateDog("Same City Small", shelterId: TestDbContextFactory.ShelterId);
        sameCitySmall.Size = DogSize.Small;
        sameCitySmall.Description = "Apartment companion dog.";
        var otherCityLarge = TestDbContextFactory.CreateDog("Other City Large", shelterId: TestDbContextFactory.OtherShelterId);
        otherCityLarge.Size = DogSize.Large;
        otherCityLarge.Description = "Companion dog.";
        context.Dogs.AddRange(sameCitySmall, otherCityLarge);
        await context.SaveChangesAsync();
        var service = CreateSemanticService(context, settings: new OpenAiSettings { Enabled = false });

        var results = await service.SearchDogsAsync("apartment companion dog", TestDbContextFactory.AdopterId, 2);

        Assert.Equal("Same City Small", results.First().Dog.Name);
    }

    [Fact]
    public async Task SemanticSearch_FiltersByShelterNeighborhood()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var testShelter = await context.Shelters.FindAsync(TestDbContextFactory.ShelterId);
        var otherShelter = await context.Shelters.FindAsync(TestDbContextFactory.OtherShelterId);
        testShelter!.Neighborhood = "Zorilor";
        otherShelter!.Neighborhood = "Manastur";
        var zorilorDog = TestDbContextFactory.CreateDog("Zorilor Medium", shelterId: TestDbContextFactory.ShelterId);
        zorilorDog.Size = DogSize.Medium;
        zorilorDog.Description = "Calm medium dog.";
        var manasturDog = TestDbContextFactory.CreateDog("Manastur Medium", shelterId: TestDbContextFactory.OtherShelterId);
        manasturDog.Size = DogSize.Medium;
        manasturDog.Description = "Calm medium dog.";
        context.Dogs.AddRange(zorilorDog, manasturDog);
        await context.SaveChangesAsync();
        var service = CreateSemanticService(context, settings: new OpenAiSettings { Enabled = false });

        var results = await service.SearchDogsAsync(
            "medium dog",
            TestDbContextFactory.AdopterId,
            options: new SemanticDogSearchOptions { Neighborhood = "Zorilor" });

        Assert.Contains(results, result => result.Dog.Name == "Zorilor Medium");
        Assert.DoesNotContain(results, result => result.Dog.Name == "Manastur Medium");
    }

    [Fact]
    public async Task SemanticSearch_UsesEmbeddingsWhenAvailable()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var calmDog = TestDbContextFactory.CreateDog("Calm Match");
        calmDog.Description = "Calm apartment dog.";
        var activeDog = TestDbContextFactory.CreateDog("Active Match");
        activeDog.Description = "Active yard dog.";
        context.Dogs.AddRange(calmDog, activeDog);
        await context.SaveChangesAsync();
        var embeddingService = new FakeEmbeddingService();
        var embeddingIndex = CreateEmbeddingIndex(context, embeddingService);
        await embeddingIndex.RefreshAllDogEmbeddingsAsync();
        var service = CreateSemanticService(context, embeddingService, EnabledOpenAiSettings());

        var results = await service.SearchDogsAsync("calm apartment dog", TestDbContextFactory.AdopterId, 2);

        Assert.Equal("Calm Match", results.First().Dog.Name);
        Assert.True(results.First().UsedSemanticEmbeddings);
    }

    [Fact]
    public async Task AdoptionCopilot_IgnoresUnknownOpenAiDogIds()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Known Dog");
        dog.Description = "Friendly dog.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = new OpenAiAdoptionCopilotResponse(true, "Known matches are ready.",
            [
                new OpenAiAdoptionCopilotItem(99999, 1, "Excellent match", 96, ["Unknown dog"], "Ignore this."),
                new OpenAiAdoptionCopilotItem(dog.Id, 2, "Good match", 82, ["Friendly profile"], "View details.")
            ])
        };
        var service = CreateCopilotService(context, fakeCopilotClient);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "friendly dog");

        Assert.Single(response.Results);
        Assert.Equal(dog.Id, response.Results[0].DogId);
        Assert.True(response.UsedAiEnhancement);
    }

    [Fact]
    public async Task AdoptionCopilot_NormalizesAwkwardAssistantMessage()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Bella");
        dog.Description = "Calm dog for an apartment.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = new OpenAiAdoptionCopilotResponse(true, "Here are the closest matches from the dogs you shared.",
            [
                new OpenAiAdoptionCopilotItem(dog.Id, 1, "Good match", 82, ["Calm apartment profile"], "View details.")
            ])
        };
        var service = CreateCopilotService(context, fakeCopilotClient);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "calm apartment dog");

        Assert.DoesNotContain("dogs you shared", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("available PawConnect dogs", response.AssistantMessage);
    }

    [Fact]
    public async Task AdoptionCopilot_AppliesExplicitNeighborhoodConstraint()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var testShelter = await context.Shelters.FindAsync(TestDbContextFactory.ShelterId);
        var otherShelter = await context.Shelters.FindAsync(TestDbContextFactory.OtherShelterId);
        testShelter!.Neighborhood = "Zorilor";
        otherShelter!.Neighborhood = "Manastur";
        var zorilorDog = TestDbContextFactory.CreateDog("Zorilor Dog", shelterId: TestDbContextFactory.ShelterId);
        zorilorDog.Size = DogSize.Medium;
        zorilorDog.Description = "Friendly medium dog.";
        var manasturDog = TestDbContextFactory.CreateDog("Manastur Dog", shelterId: TestDbContextFactory.OtherShelterId);
        manasturDog.Size = DogSize.Medium;
        manasturDog.Description = "Friendly medium dog.";
        context.Dogs.AddRange(zorilorDog, manasturDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "find me a medium dog in Zorilor");

        Assert.Contains(response.Results, result => result.Dog.Name == "Zorilor Dog");
        Assert.DoesNotContain(response.Results, result => result.Dog.Name == "Manastur Dog");
    }

    [Fact]
    public async Task AdoptionCopilot_AppliesExplicitCoatColorConstraint()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var blackDog = TestDbContextFactory.CreateDog("Black Coat Match");
        blackDog.CoatColor = "Black";
        blackDog.Description = "Gentle medium dog.";
        var goldenDog = TestDbContextFactory.CreateDog("Golden Coat Dog");
        goldenDog.CoatColor = "Golden";
        goldenDog.Description = "Gentle medium dog.";
        context.Dogs.AddRange(blackDog, goldenDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "find me a black dog");

        var blackResult = Assert.Single(response.Results, result => result.DogId == blackDog.Id);
        Assert.Equal("Exact match", blackResult.MatchLabel);
        Assert.Contains("coat", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status filter", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(response.Results, result => result.DogId == goldenDog.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Coat color" && constraint.Value.Contains("Black", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_BlackDogQueryIncludesBlackAndTanDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var blackAndTanDog = TestDbContextFactory.CreateDog("Black And Tan Match");
        blackAndTanDog.CoatColor = "Black and tan";
        var goldenDog = TestDbContextFactory.CreateDog("Golden Dog");
        goldenDog.CoatColor = "Golden";
        context.Dogs.AddRange(blackAndTanDog, goldenDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "black and tan dogs");

        var blackAndTanResult = Assert.Single(response.Results, result => result.DogId == blackAndTanDog.Id);
        Assert.Equal("Exact match", blackAndTanResult.MatchLabel);
        Assert.Contains("coat color Black and tan", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status filter", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(response.Results, result => result.DogId == goldenDog.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Coat color" && constraint.Value.Contains("Black and tan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ShortWalksQueryRanksExplicitShortWalkEvidenceHigher()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var shortWalksMatch = TestDbContextFactory.CreateDog("Short Walks Match");
        shortWalksMatch.Size = DogSize.Small;
        shortWalksMatch.Description = "Enjoys short walks, quiet routines, and settling near familiar people.";
        shortWalksMatch.BehaviorDescription = "Gentle and relaxed with volunteers.";
        shortWalksMatch.Images.Add(new DogImage
        {
            ImageUrl = "https://example.com/short-walks-match.jpg",
            IsMainImage = true
        });
        var singleShortWalkEvidence = TestDbContextFactory.CreateDog("Single Short Walk Evidence");
        singleShortWalkEvidence.Size = DogSize.Medium;
        singleShortWalkEvidence.Description = "Enjoys short walks with familiar volunteers.";
        singleShortWalkEvidence.BehaviorDescription = "Friendly after a steady introduction.";
        var mediumNoWalkEvidence = TestDbContextFactory.CreateDog("Medium No Walk Evidence");
        mediumNoWalkEvidence.Size = DogSize.Medium;
        mediumNoWalkEvidence.Description = "Friendly dog looking for a family.";
        mediumNoWalkEvidence.BehaviorDescription = "Social with familiar people.";
        var largeShortWalks = TestDbContextFactory.CreateDog("Large Short Walk Dog");
        largeShortWalks.Size = DogSize.Large;
        largeShortWalks.Description = "Enjoys short walks and quiet evenings.";
        context.Dogs.AddRange(shortWalksMatch, singleShortWalkEvidence, mediumNoWalkEvidence, largeShortWalks);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "Show me small or medium dogs that like short walks");

        var shortResult = Assert.Single(response.Results, result => result.DogId == shortWalksMatch.Id);
        var singleShortResult = Assert.Single(response.Results, result => result.DogId == singleShortWalkEvidence.Id);
        var mediumResult = Assert.Single(response.Results, result => result.DogId == mediumNoWalkEvidence.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == largeShortWalks.Id);
        Assert.Equal(shortWalksMatch.Id, response.Results.First().DogId);
        Assert.True(
            shortResult.ScorePercent >= mediumResult.ScorePercent + 8,
            $"Expected explicit short-walk evidence to score clearly higher, but got short={shortResult.ScorePercent}, medium={mediumResult.ScorePercent}.");
        Assert.True(
            shortResult.ScorePercent >= singleShortResult.ScorePercent + 3,
            $"Expected multiple short-walk indicators to outrank a single indicator, but got multi={shortResult.ScorePercent}, single={singleShortResult.ScorePercent}.");
        Assert.True(
            singleShortResult.ScorePercent > mediumResult.ScorePercent,
            $"Expected direct short-walk evidence to outrank size-only evidence, but got single={singleShortResult.ScorePercent}, medium={mediumResult.ScorePercent}.");
        Assert.Contains(shortResult.DisplayTags!, tag =>
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Size" && constraint.Value.Contains("Small", StringComparison.OrdinalIgnoreCase) && constraint.Value.Contains("Medium", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Lifestyle" && constraint.Value.Contains("Low activity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Activity" && constraint.Value.Contains("Short walks", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label == "Temperament" && constraint.Value.Contains("walk", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ApartmentLongerWalksDoesNotRewardShortWalkOnlyDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var longerWalksFit = TestDbContextFactory.CreateDog("Longer Walks Apartment Fit");
        longerWalksFit.Size = DogSize.Medium;
        longerWalksFit.Description = "Enjoys longer walks and brisk walks, then settles well indoors.";
        longerWalksFit.BehaviorDescription = "Steady and manageable in a smaller home routine.";
        var shortWalkOnly = TestDbContextFactory.CreateDog("Short Walk Only Dog");
        shortWalkOnly.Size = DogSize.Small;
        shortWalkOnly.Description = "Enjoys short daily walks, quiet routine, and indoor rest.";
        shortWalkOnly.BehaviorDescription = "Low energy and gentle with familiar people.";
        context.Dogs.AddRange(longerWalksFit, shortWalkOnly);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I live in an apartment but enjoy longer walks");

        var longerResult = Assert.Single(response.Results, result => result.DogId == longerWalksFit.Id);
        var shortResult = Assert.Single(response.Results, result => result.DogId == shortWalkOnly.Id);
        Assert.Equal(longerWalksFit.Id, response.Results.First().DogId);
        Assert.True(
            longerResult.ScorePercent >= shortResult.ScorePercent + 5,
            $"Expected longer-walk evidence to outrank short-walk-only evidence, but got longer={longerResult.ScorePercent}, short={shortResult.ScorePercent}.");
        Assert.InRange(longerResult.ScorePercent, 65, 79);
        Assert.True(
            shortResult.ScorePercent <= 60,
            $"Expected short-walk-only evidence to stay lower for a longer-walk query, but got {shortResult.ScorePercent}.");
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Home" && constraint.Value.Contains("Apartment", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Lifestyle" && constraint.Value.Contains("Moderate activity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Activity" && constraint.Value.Contains("Longer walks", StringComparison.OrdinalIgnoreCase));
        Assert.Single(response.AppliedConstraints!, constraint =>
            constraint.Label.Equals("Activity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label.Equals("Activity", StringComparison.OrdinalIgnoreCase) &&
            constraint.Value.Contains("Moderate activity", StringComparison.OrdinalIgnoreCase));
        Assert.Single(response.AppliedConstraints!, constraint =>
            constraint.Label.Equals("Lifestyle", StringComparison.OrdinalIgnoreCase) &&
            constraint.Value.Contains("Moderate activity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label == "Temperament" && constraint.Value.Contains("walk", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(longerResult.DisplayTags!, tag =>
            tag.Contains("Longer walks", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(shortResult.DisplayTags!, tag =>
            tag.Contains("Longer walks", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(shortResult.CautionTags!, tag =>
            tag.Contains("shorter walks", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.Results, result =>
            result.DisplayTags?.Any(tag => tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase)) == true);
    }

    [Fact]
    public async Task AdoptionCopilot_ApartmentLongerWalksRanksApartmentSupportAboveSpaceOnlyFit()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var spaceOnlyFit = TestDbContextFactory.CreateDog("Space Only Longer Walk Fit");
        spaceOnlyFit.Size = DogSize.Large;
        spaceOnlyFit.Description = "Enjoys longer walks and open areas with room to run.";
        spaceOnlyFit.BehaviorDescription = "Best reviewed with the shelter for smaller homes.";
        spaceOnlyFit.Images.Add(new DogImage
        {
            ImageUrl = "https://example.com/space-only-longer-walk.jpg",
            IsMainImage = true
        });

        var apartmentSupportedFit = TestDbContextFactory.CreateDog("Apartment Supported Longer Walk Fit");
        apartmentSupportedFit.Size = DogSize.Medium;
        apartmentSupportedFit.Description = "Enjoys longer walks and then settles quickly. Open areas are fun, but his medium size makes daily handling easier.";
        apartmentSupportedFit.BehaviorDescription = "Steady with familiar people.";
        apartmentSupportedFit.Images.Add(new DogImage
        {
            ImageUrl = "https://example.com/apartment-supported-longer-walk.jpg",
            IsMainImage = true
        });

        context.Dogs.AddRange(spaceOnlyFit, apartmentSupportedFit);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I live in an apartment but enjoy longer walks");

        var spaceOnlyResult = Assert.Single(response.Results, result => result.DogId == spaceOnlyFit.Id);
        var apartmentSupportedResult = Assert.Single(response.Results, result => result.DogId == apartmentSupportedFit.Id);
        Assert.Equal(apartmentSupportedFit.Id, response.Results.First().DogId);
        Assert.True(
            apartmentSupportedResult.ScorePercent > spaceOnlyResult.ScorePercent,
            $"Expected visible apartment support to outrank space-only longer-walk evidence, but got supported={apartmentSupportedResult.ScorePercent}, spaceOnly={spaceOnlyResult.ScorePercent}.");
        Assert.Contains(apartmentSupportedResult.DisplayTags!, tag =>
            tag.Contains("Longer walks", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apartmentSupportedResult.DisplayTags!, tag =>
            tag.Contains("Medium size", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apartmentSupportedResult.DisplayTags!, tag =>
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(spaceOnlyResult.CautionTags!, tag =>
            tag.Contains("Needs more space", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ApartmentLongerWalksKeepsLargeReservedDogCloseToMediumApartmentEvidence()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var buddyStyle = TestDbContextFactory.CreateDog("Buddy Style Large Longer Walk", DogStatus.Reserved);
        buddyStyle.Size = DogSize.Large;
        buddyStyle.Description = "Enjoys longer walks, fetch, and structured walks with people.";
        buddyStyle.BehaviorDescription = "Friendly and steady with familiar handlers.";
        buddyStyle.Images.Add(new DogImage
        {
            ImageUrl = "https://example.com/buddy-style-longer-walk.jpg",
            IsMainImage = true
        });

        var maxStyle = TestDbContextFactory.CreateDog("Max Style Medium Longer Walk", DogStatus.Available);
        maxStyle.Size = DogSize.Medium;
        maxStyle.Description = "Enjoys longer walks and then settles quickly. He likes open areas with room to run before resting.";
        maxStyle.BehaviorDescription = "Playful but settles after structured activity.";
        maxStyle.Images.Add(new DogImage
        {
            ImageUrl = "https://example.com/max-style-longer-walk.jpg",
            IsMainImage = true
        });

        context.Dogs.AddRange(buddyStyle, maxStyle);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I live in an apartment but enjoy longer walks");

        var buddyResult = Assert.Single(response.Results, result => result.DogId == buddyStyle.Id);
        var maxResult = Assert.Single(response.Results, result => result.DogId == maxStyle.Id);
        Assert.True(
            Math.Abs(buddyResult.ScorePercent - maxResult.ScorePercent) <= 5,
            $"Expected Buddy-style and Max-style scores to stay explainably close, but got Buddy={buddyResult.ScorePercent}, Max={maxResult.ScorePercent}.");
        Assert.True(
            maxResult.ScorePercent >= buddyResult.ScorePercent - 2,
            $"Expected Max-style apartment support to avoid a large gap below Buddy-style result, but got Buddy={buddyResult.ScorePercent}, Max={maxResult.ScorePercent}.");
        Assert.Contains(buddyResult.CautionTags!, tag =>
            tag.Contains("Large dog", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(buddyResult.CautionTags!, tag =>
            tag.Contains("Reserved", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(maxResult.DisplayTags!, tag =>
            tag.Contains("Medium size", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(maxResult.DisplayTags!, tag =>
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(maxResult.CautionTags!, tag =>
            tag.Contains("Needs more space", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ReservedStatusDoesNotHeavilyPenalizeLongerWalkApartmentFit()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var availableFit = TestDbContextFactory.CreateDog("Available Longer Walk Fit", DogStatus.Available);
        availableFit.Size = DogSize.Medium;
        availableFit.Description = "Enjoys longer walks and brisk walks, then settles quickly in a smaller home routine.";
        availableFit.BehaviorDescription = "Steady and manageable with familiar people.";
        availableFit.Images.Add(new DogImage
        {
            ImageUrl = "https://example.com/available-longer-walk.jpg",
            IsMainImage = true
        });

        var reservedFit = TestDbContextFactory.CreateDog("Reserved Longer Walk Fit", DogStatus.Reserved);
        reservedFit.Size = DogSize.Medium;
        reservedFit.Description = "Enjoys longer walks and brisk walks, then settles quickly in a smaller home routine.";
        reservedFit.BehaviorDescription = "Steady and manageable with familiar people.";
        reservedFit.Images.Add(new DogImage
        {
            ImageUrl = "https://example.com/reserved-longer-walk.jpg",
            IsMainImage = true
        });

        context.Dogs.AddRange(availableFit, reservedFit);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I live in an apartment but enjoy longer walks");

        var availableResult = Assert.Single(response.Results, result => result.DogId == availableFit.Id);
        var reservedResult = Assert.Single(response.Results, result => result.DogId == reservedFit.Id);
        Assert.True(
            availableResult.ScorePercent - reservedResult.ScorePercent <= 5,
            $"Expected reserved status to be a small availability warning, but got available={availableResult.ScorePercent}, reserved={reservedResult.ScorePercent}.");
        Assert.Contains(reservedResult.CautionTags!, tag =>
            tag.Contains("Reserved", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(reservedResult.DisplayTags!, tag =>
            tag.Contains("Longer walks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ApartmentLongerWalksKeepsSimilarEvidenceScoresClose()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var oscarStyle = TestDbContextFactory.CreateDog("Oscar Longer Walk Fit", DogStatus.Available);
        oscarStyle.Size = DogSize.Medium;
        oscarStyle.Description = "Enjoys longer walks and brisk walks, then settles quickly in a steady routine.";
        oscarStyle.BehaviorDescription = "Moderate activity suits him well.";

        var nalaStyle = TestDbContextFactory.CreateDog("Nala Longer Walk Fit", DogStatus.Reserved);
        nalaStyle.Size = DogSize.Medium;
        nalaStyle.Description = "Enjoys longer walks and brisk walks, then settles quickly in a steady routine.";
        nalaStyle.BehaviorDescription = "Moderate activity suits her well.";

        var shortWalkOnly = TestDbContextFactory.CreateDog("Low Activity Only Fit", DogStatus.Available);
        shortWalkOnly.Size = DogSize.Small;
        shortWalkOnly.Description = "Enjoys short walks, quiet routines, and indoor rest.";
        shortWalkOnly.BehaviorDescription = "Low activity and gentle with familiar people.";

        context.Dogs.AddRange(oscarStyle, nalaStyle, shortWalkOnly);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I live in an apartment but enjoy longer walks");

        var oscarResult = Assert.Single(response.Results, result => result.DogId == oscarStyle.Id);
        var nalaResult = Assert.Single(response.Results, result => result.DogId == nalaStyle.Id);
        var shortResult = Assert.Single(response.Results, result => result.DogId == shortWalkOnly.Id);
        Assert.True(
            Math.Abs(oscarResult.ScorePercent - nalaResult.ScorePercent) <= 8,
            $"Expected similar longer-walk evidence to stay close despite reserved warning, but got Oscar={oscarResult.ScorePercent}, Nala={nalaResult.ScorePercent}.");
        Assert.True(
            oscarResult.ScorePercent >= shortResult.ScorePercent + 5,
            $"Expected longer-walk evidence to outrank short-walk-only evidence, but got Oscar={oscarResult.ScorePercent}, short={shortResult.ScorePercent}.");
        Assert.True(
            nalaResult.ScorePercent >= shortResult.ScorePercent + 3,
            $"Expected reserved longer-walk evidence to remain above short-walk-only evidence, but got Nala={nalaResult.ScorePercent}, short={shortResult.ScorePercent}.");
    }

    [Fact]
    public async Task AdoptionCopilot_CalmDogsInZorilorStayPublicSafeAndPreferCalmMatches()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var testShelter = await context.Shelters.FindAsync(TestDbContextFactory.ShelterId);
        var otherShelter = await context.Shelters.FindAsync(TestDbContextFactory.OtherShelterId);
        testShelter!.Neighborhood = "Zorilor";
        otherShelter!.Neighborhood = "Manastur";
        var calmZorilor = TestDbContextFactory.CreateDog("Calm Zorilor", shelterId: TestDbContextFactory.ShelterId);
        calmZorilor.BehaviorDescription = "Calm, quiet, and gentle companion.";
        var activeZorilor = TestDbContextFactory.CreateDog("Active Zorilor", shelterId: TestDbContextFactory.ShelterId);
        activeZorilor.BehaviorDescription = "Energetic dog who loves running.";
        var calmManastur = TestDbContextFactory.CreateDog("Calm Manastur", shelterId: TestDbContextFactory.OtherShelterId);
        calmManastur.BehaviorDescription = "Calm and gentle dog.";
        var adoptedZorilor = TestDbContextFactory.CreateDog("Adopted Zorilor", DogStatus.Adopted, TestDbContextFactory.ShelterId);
        adoptedZorilor.BehaviorDescription = "Calm adopted dog.";
        context.Dogs.AddRange(calmZorilor, activeZorilor, calmManastur, adoptedZorilor);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "calm dogs in Zorilor");

        Assert.NotEmpty(response.Results);
        Assert.Equal(calmZorilor.Id, response.Results.First().DogId);
        Assert.All(response.Results, result => Assert.Equal("Zorilor", result.Dog.Shelter!.Neighborhood));
        Assert.DoesNotContain(response.Results, result => result.DogId == activeZorilor.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == adoptedZorilor.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Temperament" && constraint.Value.Contains("Calm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Lifestyle" && constraint.Value.Contains("Low activity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Status" && constraint.Value.Contains("Available", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ApartmentQueryBoostsCalmSmallOrMediumDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var calmSmall = TestDbContextFactory.CreateDog("Calm Apartment Match");
        calmSmall.Size = DogSize.Small;
        calmSmall.BehaviorDescription = "Calm, quiet, low energy dog suited for apartment living.";
        var activeLarge = TestDbContextFactory.CreateDog("Active Yard Dog");
        activeLarge.Size = DogSize.Large;
        activeLarge.BehaviorDescription = "Active energetic dog who loves a yard and outdoor space.";
        context.Dogs.AddRange(calmSmall, activeLarge);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "find me a calm dog for an apartment");

        Assert.NotEmpty(response.Results);
        Assert.Equal(calmSmall.Id, response.Results.First().DogId);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Home" && constraint.Value.Contains("Apartment", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Results.First().Reasons, reason =>
            reason.Contains("Apartment", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("Calm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Home" && constraint.Value.Contains("Apartment", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Temperament" && constraint.Value.Contains("Calm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ApartmentLowActivityQueryUsesCleanChipsAndSpecificReasons()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var availableFit = TestDbContextFactory.CreateDog("Available Apartment Fit", DogStatus.Available);
        availableFit.Size = DogSize.Medium;
        availableFit.Description = "Enjoys short daily walks, indoor rest, and a quiet routine. Settles quickly after exploring and likes relaxed evenings.";
        availableFit.BehaviorDescription = "Gentle and steady with familiar volunteers.";
        var reservedFit = TestDbContextFactory.CreateDog("Reserved Apartment Fit", DogStatus.Reserved);
        reservedFit.Size = DogSize.Medium;
        reservedFit.Description = "Enjoys short daily walks, indoor rest, and a quiet routine. Settles quickly after exploring.";
        reservedFit.BehaviorDescription = "Gentle and steady with familiar volunteers.";
        var genericFriendly = TestDbContextFactory.CreateDog("Generic Friendly Large", DogStatus.Available);
        genericFriendly.Size = DogSize.Large;
        genericFriendly.Description = "Friendly dog looking for a family.";
        genericFriendly.BehaviorDescription = "Friendly with people.";
        context.Dogs.AddRange(availableFit, reservedFit, genericFriendly);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I live in an apartment and I want a dog that doesn't need too much activity.");

        var availableResult = Assert.Single(response.Results, result => result.DogId == availableFit.Id);
        var reservedResult = Assert.Single(response.Results, result => result.DogId == reservedFit.Id);
        var genericResult = Assert.Single(response.Results, result => result.DogId == genericFriendly.Id);
        Assert.Equal(availableFit.Id, response.Results.First().DogId);
        Assert.True(
            availableResult.ScorePercent > reservedResult.ScorePercent,
            $"Expected available score to exceed reserved score, but got available={availableResult.ScorePercent}, reserved={reservedResult.ScorePercent}.");
        Assert.True(availableResult.ScorePercent <= 86);
        Assert.DoesNotContain(response.Results, result => result.MatchLabel is "Excellent match" or "Potential match" or "Weak match");
        Assert.Contains(availableResult.MatchLabel, new[] { "Strong match", "Good match", "Possible match", "Low match" });
        Assert.NotEqual("Strong match", genericResult.MatchLabel);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Home" && constraint.Value.Contains("Apartment", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Lifestyle" && constraint.Value.Contains("Low activity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label == "Behavior" || constraint.Value.Contains("apartment", StringComparison.OrdinalIgnoreCase) && constraint.Label == "Temperament");
        Assert.Contains(availableResult.DisplayTags!, tag =>
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Indoor rest", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Quiet routine", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(availableResult.DisplayTags!, tag =>
            tag.Contains("Friendly temperament", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(reservedResult.CautionTags!, tag =>
            tag.Contains("Reserved", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_CautionSignalsDifferentiateOtherwiseSimilarApartmentMatches()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var shelter = await context.Shelters.FindAsync(TestDbContextFactory.ShelterId);
        shelter!.City = "Cluj-Napoca";
        var steadyFit = TestDbContextFactory.CreateDog("Steady Apartment Fit", DogStatus.Available);
        steadyFit.Size = DogSize.Medium;
        steadyFit.Description = "Enjoys short walks, indoor rest, and a quiet routine. Settles quickly after exploring.";
        steadyFit.BehaviorDescription = "Gentle and steady with familiar volunteers.";
        var cautiousFit = TestDbContextFactory.CreateDog("Cautious Apartment Fit", DogStatus.Available);
        cautiousFit.Size = DogSize.Medium;
        cautiousFit.Description = "Enjoys short walks, indoor rest, and a quiet routine. Settles quickly after exploring.";
        cautiousFit.BehaviorDescription = "Needs slow introductions and a patient adopter before relaxing.";
        context.Dogs.AddRange(steadyFit, cautiousFit);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "Find me a calm medium-sized dog in Cluj-Napoca that can live in an apartment");

        var steadyResult = Assert.Single(response.Results, result => result.DogId == steadyFit.Id);
        var cautiousResult = Assert.Single(response.Results, result => result.DogId == cautiousFit.Id);
        Assert.True(
            steadyResult.ScorePercent >= cautiousResult.ScorePercent + 5,
            $"Expected caution to lower score by at least 5, but got steady={steadyResult.ScorePercent}, cautious={cautiousResult.ScorePercent}.");
        Assert.Contains(cautiousResult.CautionTags!, tag =>
            tag.Contains("Patient adopter needed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_StrongerCautionSignalsLowerApartmentFitScore()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var shelter = await context.Shelters.FindAsync(TestDbContextFactory.ShelterId);
        shelter!.City = "Cluj-Napoca";

        var oscarStyle = TestDbContextFactory.CreateDog("Oscar Style Apartment Candidate", DogStatus.Available);
        oscarStyle.Size = DogSize.Medium;
        oscarStyle.Description = "Settles quickly after a medium walk and enjoys a steady, calmer routine.";
        oscarStyle.BehaviorDescription = "Gentle with familiar people, but the shelter should confirm apartment fit.";

        var maxStyle = TestDbContextFactory.CreateDog("Max Style Higher Activity Candidate", DogStatus.Available);
        maxStyle.Size = DogSize.Medium;
        maxStyle.Description = "Enjoys longer walks, outdoor play, and open areas with room to run before settling.";
        maxStyle.BehaviorDescription = "Energetic and playful, with structured activity helping him settle.";

        var sashaStyle = TestDbContextFactory.CreateDog("Sasha Style Cautious Candidate", DogStatus.Reserved);
        sashaStyle.Size = DogSize.Medium;
        sashaStyle.Description = "Settles after short walks but needs a patient adopter before relaxing in a new routine.";
        sashaStyle.BehaviorDescription = "Needs slow introductions and patient handling.";

        context.Dogs.AddRange(oscarStyle, maxStyle, sashaStyle);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "Find me a calm medium-sized dog in Cluj-Napoca that can live in an apartment");

        var oscarResult = Assert.Single(response.Results, result => result.DogId == oscarStyle.Id);
        var maxResult = Assert.Single(response.Results, result => result.DogId == maxStyle.Id);
        var sashaResult = Assert.Single(response.Results, result => result.DogId == sashaStyle.Id);

        Assert.True(
            oscarResult.ScorePercent >= maxResult.ScorePercent + 2,
            $"Expected stronger activity/space cautions to lower Max-style score, but got Oscar={oscarResult.ScorePercent}, Max={maxResult.ScorePercent}.");
        Assert.True(
            sashaResult.ScorePercent <= 55,
            $"Expected patient/reserved caution to keep Sasha-style score in the lower possible band, but got Sasha={sashaResult.ScorePercent}.");
        Assert.Contains(maxResult.CautionTags!, tag =>
            tag.Contains("Higher activity needs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(maxResult.CautionTags!, tag =>
            tag.Contains("Needs more space", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sashaResult.CautionTags!, tag =>
            tag.Contains("Patient adopter needed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_YardQueryBoostsActiveLargeDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context, housingType: HousingType.House, hasYard: true);
        var activeLarge = TestDbContextFactory.CreateDog("Active Yard Match");
        activeLarge.Size = DogSize.Large;
        activeLarge.BehaviorDescription = "Active, playful, energetic dog who enjoys yard and garden time.";
        var calmSmall = TestDbContextFactory.CreateDog("Calm Apartment Dog");
        calmSmall.Size = DogSize.Small;
        calmSmall.BehaviorDescription = "Calm low energy apartment companion.";
        var quietIndoor = TestDbContextFactory.CreateDog("Quiet Indoor Dog");
        quietIndoor.Size = DogSize.Small;
        quietIndoor.Description = "Likes leash walks and quiet indoor rest.";
        quietIndoor.BehaviorDescription = "Friendly once introduced slowly. More comfortable with calm dogs than very energetic ones.";
        var mediumIndoor = TestDbContextFactory.CreateDog("Medium Indoor Dog");
        mediumIndoor.Size = DogSize.Medium;
        mediumIndoor.Description = "Enjoys short daily walks, gentle play, and indoor rest after time with people.";
        mediumIndoor.BehaviorDescription = "Friendly and attentive around people.";
        context.Dogs.AddRange(activeLarge, calmSmall, quietIndoor, mediumIndoor);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I want an active dog for a house with a yard");

        Assert.NotEmpty(response.Results);
        Assert.Equal(activeLarge.Id, response.Results.First().DogId);
        var quietIndoorResult = Assert.Single(response.Results, result => result.DogId == quietIndoor.Id);
        Assert.DoesNotContain(quietIndoorResult.Reasons, reason =>
            reason.Contains("Yard", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("Outdoor", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("Active lifestyle", StringComparison.OrdinalIgnoreCase));
        var mediumIndoorResult = Assert.Single(response.Results, result => result.DogId == mediumIndoor.Id);
        Assert.DoesNotContain(mediumIndoorResult.Reasons, reason =>
            reason.Contains("Yard", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("Outdoor", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("Active lifestyle", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Lifestyle" && constraint.Value.Contains("Active", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Home" && constraint.Value.Contains("House with yard", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_OpenAiDisabledStillHandlesFriendlyMediumBeginnerQuery()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var friendlyMedium = TestDbContextFactory.CreateDog("Beginner Medium Match");
        friendlyMedium.Size = DogSize.Medium;
        friendlyMedium.BehaviorDescription = "Friendly, gentle, easy dog for a beginner.";
        var largeActive = TestDbContextFactory.CreateDog("Large Active Dog");
        largeActive.Size = DogSize.Large;
        largeActive.BehaviorDescription = "Energetic dog for experienced adopters.";
        context.Dogs.AddRange(friendlyMedium, largeActive);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "friendly medium dog for a beginner");

        Assert.NotEmpty(response.Results);
        Assert.Equal(friendlyMedium.Id, response.Results.First().DogId);
        Assert.DoesNotContain(response.Results, result => result.DogId == largeActive.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Size" && constraint.Value.Contains("Medium", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Temperament" && constraint.Value.Contains("Beginner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ReturnsFriendlyMessageWhenExplicitNeighborhoodHasNoDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Other Dog", shelterId: TestDbContextFactory.OtherShelterId);
        dog.Description = "Friendly medium dog.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "find me a medium dog in Zorilor");

        Assert.Empty(response.Results);
        Assert.Equal("No dogs found in Zorilor. Try nearby search or a larger area.", response.AssistantMessage);
    }

    [Fact]
    public async Task AdoptionCopilot_AsksForSpecificNeighborhoodWhenNeighborhoodIntentIsVague()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var largeDog = TestDbContextFactory.CreateDog("Large Dog");
        largeDog.Size = DogSize.Large;
        context.Dogs.Add(largeDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "Find a large dog in a neighborhood with no dogs");

        Assert.Empty(response.Results);
        Assert.Contains("Please name a specific neighborhood", response.AssistantMessage);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Size" && constraint.Value.Contains("Large", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_DeterministicSizeConstraintOverridesIncompleteToolArguments()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var mediumDog = TestDbContextFactory.CreateDog("Medium Match");
        mediumDog.Size = DogSize.Medium;
        mediumDog.Description = "Friendly medium dog.";
        var smallDog = TestDbContextFactory.CreateDog("Small Nonmatch");
        smallDog.Size = DogSize.Small;
        smallDog.Description = "Friendly small dog.";
        context.Dogs.AddRange(mediumDog, smallDog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = new OpenAiAdoptionCopilotResponse(true, "Medium matches are ready.",
            [
                new OpenAiAdoptionCopilotItem(smallDog.Id, 1, "Excellent match", 95, ["Friendly profile"], "Ignore this."),
                new OpenAiAdoptionCopilotItem(mediumDog.Id, 2, "Good match", 82, ["Medium size"], "View details.")
            ])
        };
        fakeCopilotClient.ToolCalls.Add(new OpenAiCopilotToolCall("call_search", "search_dogs", """{"query":"friendly dog","count":8}"""));
        var service = CreateCopilotService(context, fakeCopilotClient);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "medium size dog");

        Assert.Contains(response.Results, result => result.DogId == mediumDog.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == smallDog.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Size" && constraint.Value.Contains("Medium", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_DeterministicReservedStatusConstraintOverridesIncompleteToolArguments()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var reservedDog = TestDbContextFactory.CreateDog("Reserved Match", DogStatus.Reserved);
        var availableDog = TestDbContextFactory.CreateDog("Available Nonmatch", DogStatus.Available);
        context.Dogs.AddRange(reservedDog, availableDog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = new OpenAiAdoptionCopilotResponse(true, "Reserved matches are ready.",
            [
                new OpenAiAdoptionCopilotItem(availableDog.Id, 1, "Excellent match", 95, ["Friendly profile"], "Ignore this."),
                new OpenAiAdoptionCopilotItem(reservedDog.Id, 2, "Good match", 82, ["Reserved status"], "View details.")
            ])
        };
        fakeCopilotClient.ToolCalls.Add(new OpenAiCopilotToolCall("call_search", "search_dogs", """{"query":"dog","count":8}"""));
        var service = CreateCopilotService(context, fakeCopilotClient);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "show me reserved dogs");

        Assert.Contains(response.Results, result => result.DogId == reservedDog.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == availableDog.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Status" && constraint.Value.Contains("Reserved", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_DeterministicAgeConstraintOverridesIncompleteToolArguments()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var youngDog = TestDbContextFactory.CreateDog("Young Match", ageYears: 0, ageMonths: 8);
        var olderDog = TestDbContextFactory.CreateDog("Older Nonmatch", ageYears: 2);
        context.Dogs.AddRange(youngDog, olderDog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = OpenAiAdoptionCopilotResponse.Failed("force fallback")
        };
        fakeCopilotClient.ToolCalls.Add(new OpenAiCopilotToolCall("call_search", "search_dogs", """{"query":"dog","count":8}"""));
        var service = CreateCopilotService(context, fakeCopilotClient);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "show me dogs younger than 1 year");

        Assert.Contains(response.Results, result => result.DogId == youngDog.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == olderDog.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Age" && constraint.Value.Contains("under 1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_UnderAgeConstraintExcludesDogsAtBoundary()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var underTwo = TestDbContextFactory.CreateDog("Under Two", ageYears: 1, ageMonths: 11);
        var exactlyTwo = TestDbContextFactory.CreateDog("Exactly Two", ageYears: 2, ageMonths: 0);
        var overTwo = TestDbContextFactory.CreateDog("Over Two", ageYears: 2, ageMonths: 6);
        context.Dogs.AddRange(underTwo, exactlyTwo, overTwo);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "find dogs under 2 years old");

        Assert.Contains(response.Results, result => result.DogId == underTwo.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == exactlyTwo.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == overTwo.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Age" && constraint.Value == "under 2 years");
        Assert.Contains(response.Results.Single().MatchedCriteria!, criterion =>
            criterion.Label == "Age" && criterion.Value.Contains("1 year, 11 months", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_MaxAgeConstraintIncludesExactYearOnly()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var underTwo = TestDbContextFactory.CreateDog("Under Two", ageYears: 1, ageMonths: 8);
        var exactlyTwo = TestDbContextFactory.CreateDog("Exactly Two", ageYears: 2, ageMonths: 0);
        var overTwo = TestDbContextFactory.CreateDog("Over Two", ageYears: 2, ageMonths: 6);
        context.Dogs.AddRange(underTwo, exactlyTwo, overTwo);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "max 2 years");

        Assert.Contains(response.Results, result => result.DogId == underTwo.Id);
        Assert.Contains(response.Results, result => result.DogId == exactlyTwo.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == overTwo.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Age" && constraint.Value == "max 2 years");
    }

    [Fact]
    public async Task AdoptionCopilot_OrYoungerAgeConstraintIncludesExactYearOnly()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var exactlyTwo = TestDbContextFactory.CreateDog("Exactly Two", ageYears: 2, ageMonths: 0);
        var overTwo = TestDbContextFactory.CreateDog("Over Two", ageYears: 2, ageMonths: 6);
        context.Dogs.AddRange(exactlyTwo, overTwo);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "2 years or younger");

        Assert.Contains(response.Results, result => result.DogId == exactlyTwo.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == overTwo.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Age" && constraint.Value == "max 2 years");
    }

    [Fact]
    public async Task AdoptionCopilot_ResultMatchedCriteriaIncludeSizeAndNeighborhood()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var shelter = await context.Shelters.FindAsync(TestDbContextFactory.ShelterId);
        shelter!.Neighborhood = "Zorilor";
        var mediumDog = TestDbContextFactory.CreateDog("Medium Zorilor", shelterId: TestDbContextFactory.ShelterId);
        mediumDog.Size = DogSize.Medium;
        context.Dogs.Add(mediumDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "medium dog in Zorilor");

        var result = Assert.Single(response.Results);
        Assert.Contains(result.MatchedCriteria!, criterion => criterion.Label == "Size" && criterion.Value == "Medium");
        Assert.Contains(result.MatchedCriteria!, criterion => criterion.Label == "Location" && criterion.Value == "Zorilor");
    }

    [Fact]
    public async Task AdoptionCopilot_UsesConstrainedToolSearchWhenOpenAiFinalResponseFails()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var labradorDog = TestDbContextFactory.CreateDog("Labrador Match");
        labradorDog.Breed = "Labrador Mix";
        var beagleDog = TestDbContextFactory.CreateDog("Beagle Nonmatch");
        beagleDog.Breed = "Beagle";
        context.Dogs.AddRange(labradorDog, beagleDog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = OpenAiAdoptionCopilotResponse.Failed("force fallback")
        };
        fakeCopilotClient.ToolCalls.Add(new OpenAiCopilotToolCall(
            "call_search",
            "search_dogs",
            """{"query":"labrador dog","breeds":["Labrador"],"count":8}"""));
        var service = CreateCopilotService(context, fakeCopilotClient);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "show me labrador dogs");

        Assert.Contains(response.Results, result => result.DogId == labradorDog.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == beagleDog.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Breed" && constraint.Value.Contains("Labrador", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_IgnoresOpenAiDogIdsOutsideLatestToolSearch()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var labradorDog = TestDbContextFactory.CreateDog("Labrador Match");
        labradorDog.Breed = "Labrador Mix";
        var beagleDog = TestDbContextFactory.CreateDog("Beagle Nonmatch");
        beagleDog.Breed = "Beagle";
        context.Dogs.AddRange(labradorDog, beagleDog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = new OpenAiAdoptionCopilotResponse(true, "Beagle should not pass.",
            [
                new OpenAiAdoptionCopilotItem(beagleDog.Id, 1, "Excellent match", 95, ["Wrong breed"], "Ignore this.")
            ])
        };
        fakeCopilotClient.ToolCalls.Add(new OpenAiCopilotToolCall(
            "call_search",
            "search_dogs",
            """{"query":"labrador dog","breeds":["Labrador"],"count":8}"""));
        var service = CreateCopilotService(context, fakeCopilotClient);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "show me labrador dogs");

        Assert.Contains(response.Results, result => result.DogId == labradorDog.Id);
        Assert.DoesNotContain(response.Results, result => result.DogId == beagleDog.Id);
        Assert.False(response.UsedAiEnhancement);
        Assert.Equal("OpenAI returned no valid PawConnect dog IDs.", response.FallbackReason);
    }

    [Fact]
    public async Task AdoptionCopilot_DoesNotTrustUnsupportedOpenAiYardReasons()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var quietIndoor = TestDbContextFactory.CreateDog("Quiet Indoor Dog");
        quietIndoor.Size = DogSize.Small;
        quietIndoor.Description = "Likes leash walks and quiet indoor rest.";
        quietIndoor.BehaviorDescription = "Friendly once introduced slowly. More comfortable with calm dogs than very energetic ones.";
        context.Dogs.Add(quietIndoor);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = new OpenAiAdoptionCopilotResponse(true, "Quiet dog returned.",
            [
                new OpenAiAdoptionCopilotItem(quietIndoor.Id, 1, "Excellent match", 95, ["Outdoor play", "Yard fit"], "View details.")
            ])
        };
        fakeCopilotClient.ToolCalls.Add(new OpenAiCopilotToolCall(
            "call_search",
            "search_dogs",
            """{"query":"active dog for a house with a yard","energyLevel":"High","homeType":"House with yard","yardFriendly":true,"count":8}"""));
        var service = CreateCopilotService(context, fakeCopilotClient, settings: EnabledOpenAiSettings());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I want an active dog for a house with a yard");

        var result = Assert.Single(response.Results, item => item.DogId == quietIndoor.Id);
        Assert.DoesNotContain(result.Reasons, reason =>
            reason.Contains("Yard", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("Outdoor", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("Strong match", result.MatchLabel);
    }

    [Fact]
    public async Task AdoptionCopilot_ChildrenQueryRequiresChildSpecificEvidence()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var childEvidence = TestDbContextFactory.CreateDog("Child Evidence Dog");
        childEvidence.Description = "Has done well around older children during supervised visits.";
        childEvidence.BehaviorDescription = "Gentle and attentive with patient handling.";
        var genericFamily = TestDbContextFactory.CreateDog("Generic Family Dog");
        genericFamily.Description = "Friendly dog looking for an active family.";
        genericFamily.BehaviorDescription = "Social with familiar volunteers.";
        context.Dogs.AddRange(childEvidence, genericFamily);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "dog good with children");

        var childResult = Assert.Single(response.Results, result => result.DogId == childEvidence.Id);
        var genericResult = response.Results.FirstOrDefault(result => result.DogId == genericFamily.Id);
        Assert.Contains(childResult.DisplayTags!, tag => tag.Contains("older children", StringComparison.OrdinalIgnoreCase));
        if (genericResult is not null)
        {
            Assert.Contains(genericResult.DisplayTags!, tag => tag.Contains("Ask shelter about children", StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual("Strong match", genericResult.MatchLabel);
        }
    }

    [Fact]
    public async Task AdoptionCopilot_CatQueryDoesNotTreatGenericSocialTextAsPetFriendly()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var catEvidence = TestDbContextFactory.CreateDog("Cat Evidence Dog");
        catEvidence.Description = "Comfortable with cats after calm introductions.";
        catEvidence.BehaviorDescription = "Gentle with quiet handling.";
        var shelterCatEvidence = TestDbContextFactory.CreateDog("Shelter Cat Calm Dog");
        shelterCatEvidence.Description = "During feeding time she has passed the shelter cats calmly, then returned attention to the handler.";
        shelterCatEvidence.BehaviorDescription = "Gentle handling suits her well.";
        var settlesNoCat = TestDbContextFactory.CreateDog("Settles No Cat Dog");
        settlesNoCat.Description = "Enjoys short walks, settles quickly, and feels comfortable in a quiet home.";
        settlesNoCat.BehaviorDescription = "Friendly and attentive around people.";
        var socialOnly = TestDbContextFactory.CreateDog("Social Only Dog");
        socialOnly.Description = "Friendly and social with people.";
        socialOnly.BehaviorDescription = "Enjoys visitor attention.";
        context.Dogs.AddRange(catEvidence, shelterCatEvidence, settlesNoCat, socialOnly);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "dog good with cats");

        var catResult = Assert.Single(response.Results, result => result.DogId == catEvidence.Id);
        var shelterCatResult = Assert.Single(response.Results, result => result.DogId == shelterCatEvidence.Id);
        var settlesNoCatResult = Assert.Single(response.Results, result => result.DogId == settlesNoCat.Id);
        var socialResult = Assert.Single(response.Results, result => result.DogId == socialOnly.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Compatibility" && constraint.Value.Contains("Cats", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label == "Compatibility" && constraint.Value.Contains("Other dogs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label == "Temperament" && constraint.Value.Contains("introduction", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(catResult.DisplayTags!, tag => tag.Contains("cat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(shelterCatResult.DisplayTags!, tag => tag == "Calm near cats");
        Assert.All(shelterCatResult.DisplayTags!, tag => Assert.Contains("cat", tag, StringComparison.OrdinalIgnoreCase));
        Assert.True(shelterCatResult.ScorePercent > settlesNoCatResult.ScorePercent);
        Assert.True(shelterCatResult.ScorePercent > socialResult.ScorePercent);
        Assert.NotEqual("Low match", shelterCatResult.MatchLabel);
        Assert.DoesNotContain(settlesNoCatResult.DisplayTags!, tag =>
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Indoor rest", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(settlesNoCatResult.DisplayTags!, tag =>
            tag.Contains("Calm near cats", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("slow cat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(settlesNoCatResult.DisplayTags!, tag =>
            tag.Contains("Ask shelter about cats", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(socialResult.DisplayTags!, tag =>
            tag.Contains("Calm near cats", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("slow cat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_KidsQueryUsesChildrenIntentWithoutOtherDogContext()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var childEvidence = TestDbContextFactory.CreateDog("Kids Evidence Dog");
        childEvidence.Description = "Has stayed relaxed during supervised visits with older children.";
        childEvidence.BehaviorDescription = "Takes treats gently and enjoys predictable family routines.";
        var genericDog = TestDbContextFactory.CreateDog("Generic People Dog");
        genericDog.Description = "Friendly dog looking for attention from visitors.";
        genericDog.BehaviorDescription = "Social with familiar volunteers.";
        context.Dogs.AddRange(childEvidence, genericDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have kids");

        var childResult = Assert.Single(response.Results, result => result.DogId == childEvidence.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Compatibility" && constraint.Value.Contains("Children", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label == "Compatibility" && constraint.Value.Contains("Other dogs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(childResult.DisplayTags!, tag =>
            tag.Contains("older children", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Family", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Gentle", StringComparison.OrdinalIgnoreCase));
        var genericResult = response.Results.FirstOrDefault(result => result.DogId == genericDog.Id);
        if (genericResult is not null)
        {
            Assert.NotEqual("Strong match", genericResult.MatchLabel);
            Assert.Contains(genericResult.DisplayTags!, tag =>
                tag.Contains("Ask shelter about children", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task AdoptionCopilot_OlderHouseholdDogPrefersCalmDogCompany()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var calmCompanion = TestDbContextFactory.CreateDog("Calm Companion Dog");
        calmCompanion.Description = "Enjoys short walks, a quiet routine, and settling near familiar people.";
        calmCompanion.BehaviorDescription = "Walks politely beside familiar calm dogs and takes gentle handling well.";
        var bouncyDog = TestDbContextFactory.CreateDog("Bouncy Play Dog");
        bouncyDog.Description = "Enjoys outdoor play, fetch, and space to run before settling.";
        bouncyDog.BehaviorDescription = "Can overwhelm shy dogs and prefers very energetic playmates.";
        context.Dogs.AddRange(calmCompanion, bouncyDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have an older dog at home");

        var calmResult = Assert.Single(response.Results, result => result.DogId == calmCompanion.Id);
        Assert.Equal(calmCompanion.Id, response.Results.First().DogId);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Compatibility" && constraint.Value.Contains("Senior dog", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Lifestyle" && constraint.Value.Contains("Calm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(calmResult.DisplayTags!, tag =>
            tag.Contains("Calm dog company", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Respectful around dogs", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Not too energetic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(calmResult.DisplayTags!, tag =>
            tag.Contains("Indoor rest", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase));
        var bouncyResult = response.Results.SingleOrDefault(result => result.DogId == bouncyDog.Id);
        if (bouncyResult is not null)
        {
            Assert.True(calmResult.ScorePercent > bouncyResult.ScorePercent);
            Assert.Contains(bouncyResult.CautionTags!, tag =>
                tag.Contains("sensitive dogs", StringComparison.OrdinalIgnoreCase) ||
                tag.Contains("Very energetic", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task AdoptionCopilot_SickRecoveringHouseholdDogUsesSensitiveDogIntent()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var calmCompanion = TestDbContextFactory.CreateDog("Sensitive Companion Dog");
        calmCompanion.Description = "Enjoys a quiet routine and settles near familiar people after short walks.";
        calmCompanion.BehaviorDescription = "Calm dogs are easier for her than pushy playmates.";
        var genericDog = TestDbContextFactory.CreateDog("Generic Friendly Dog");
        genericDog.Description = "Friendly dog looking for attention from visitors.";
        genericDog.BehaviorDescription = "Social with familiar volunteers.";
        context.Dogs.AddRange(calmCompanion, genericDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a sick dog recovering at home");

        var calmResult = Assert.Single(response.Results, result => result.DogId == calmCompanion.Id);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Compatibility" && constraint.Value.Contains("Sensitive dog", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Lifestyle" && constraint.Value.Contains("Calm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(calmResult.DisplayTags!, tag =>
            tag.Contains("Calm dog company", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Needs calm dog introductions", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Not suited to pushy dogs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(calmResult.DisplayTags!, tag =>
            tag.Contains("Indoor rest", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase));
        var genericResult = response.Results.FirstOrDefault(result => result.DogId == genericDog.Id);
        if (genericResult is not null)
        {
            Assert.NotEqual("Strong match", genericResult.MatchLabel);
        }
    }

    [Fact]
    public async Task AdoptionCopilot_SensitiveDogQueryCapsIndirectCalmEvidenceBelowExcellent()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var directDogEvidence = TestDbContextFactory.CreateDog("Direct Dog Evidence");
        directDogEvidence.Description = "Enjoys a steady routine and calm handling.";
        directDogEvidence.BehaviorDescription = "Walks politely beside familiar calm dogs and avoids pushy playmates.";
        var indirectCalmDog = TestDbContextFactory.CreateDog("Indirect Calm Dog");
        indirectCalmDog.Description = "Enjoys short walks, a quiet routine, and settling near familiar people.";
        indirectCalmDog.BehaviorDescription = "Friendly and gentle with people.";
        context.Dogs.AddRange(directDogEvidence, indirectCalmDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a sick dog recovering at home");

        var directResult = Assert.Single(response.Results, result => result.DogId == directDogEvidence.Id);
        var indirectResult = Assert.Single(response.Results, result => result.DogId == indirectCalmDog.Id);
        Assert.True(directResult.ScorePercent > indirectResult.ScorePercent);
        Assert.NotEqual("Strong match", indirectResult.MatchLabel);
        Assert.True(indirectResult.ScorePercent <= 82);
        Assert.Contains(indirectResult.DisplayTags!, tag =>
            tag.Contains("Ask shelter about sensitive dog", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(indirectResult.DisplayTags!, tag =>
            tag.Contains("Indoor rest", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_SensitiveDogQueryKeepsReservedDirectEvidenceCappedAndWarned()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var reservedDog = TestDbContextFactory.CreateDog("Reserved Calm Dog", DogStatus.Reserved);
        reservedDog.Description = "Enjoys a predictable routine and relaxed evenings.";
        reservedDog.BehaviorDescription = "Walks politely beside familiar calm dogs, and introductions should stay calm.";
        context.Dogs.Add(reservedDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a sick dog recovering at home");

        var result = Assert.Single(response.Results, item => item.DogId == reservedDog.Id);
        Assert.True(result.ScorePercent <= 89);
        Assert.Contains(result.CautionTags!, tag =>
            tag.Contains("Reserved", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_SensitiveDogQueryDoesNotShowGentlePlayWithoutPlayEvidence()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var gentleHandlingDog = TestDbContextFactory.CreateDog("Gentle Handling Dog");
        gentleHandlingDog.Description = "Likes quiet attention and predictable daily routines.";
        gentleHandlingDog.BehaviorDescription = "Gentle and patient during handling with familiar people.";
        context.Dogs.Add(gentleHandlingDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a sick dog recovering at home");

        var result = Assert.Single(response.Results, item => item.DogId == gentleHandlingDog.Id);
        Assert.DoesNotContain(result.DisplayTags!, tag =>
            tag.Contains("Gentle play style", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("Strong match", result.MatchLabel);
    }

    [Fact]
    public async Task AdoptionCopilot_SensitiveDogQueryDoesNotTreatGenericGentlePlayAsDogToDogEvidence()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var nalaStyleDog = TestDbContextFactory.CreateDog("Nala Style Dog");
        nalaStyleDog.Description = "Enjoys short daily walks, gentle play, and indoor rest after she has had time with people.";
        nalaStyleDog.BehaviorDescription = "Friendly and attentive around people. She has done well around older children during supervised visits.";
        context.Dogs.Add(nalaStyleDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a sick dog recovering at home");

        var result = Assert.Single(response.Results, item => item.DogId == nalaStyleDog.Id);
        Assert.NotEqual("Strong match", result.MatchLabel);
        Assert.True(result.ScorePercent <= 82);
        Assert.DoesNotContain(result.DisplayTags!, tag =>
            tag.Contains("Gentle play style", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.DisplayTags!, tag =>
            tag.Contains("Ask shelter about sensitive dog", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ChildrenQueryDoesNotShowUnrelatedLifestyleOrDogTags()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var noChildEvidence = TestDbContextFactory.CreateDog("Quiet Dog Without Child Notes");
        noChildEvidence.Description = "Likes short walks, indoor rest, and quiet routines.";
        noChildEvidence.BehaviorDescription = "Walks politely beside familiar calm dogs.";
        var olderChildEvidence = TestDbContextFactory.CreateDog("Older Child Evidence Dog");
        olderChildEvidence.Description = "Settles near familiar people after short walks.";
        olderChildEvidence.BehaviorDescription = "Has done well around older children during supervised visits.";
        context.Dogs.AddRange(noChildEvidence, olderChildEvidence);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have young children at home and need a gentle dog for a calm family routine");

        var noEvidenceResult = Assert.Single(response.Results, item => item.DogId == noChildEvidence.Id);
        var childEvidenceResult = Assert.Single(response.Results, item => item.DogId == olderChildEvidence.Id);
        Assert.NotEqual("Strong match", noEvidenceResult.MatchLabel);
        Assert.DoesNotContain(noEvidenceResult.DisplayTags!, tag =>
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Indoor rest", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Calm dog company", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(noEvidenceResult.DisplayTags!, tag =>
            tag.Contains("Ask shelter about children", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(childEvidenceResult.DisplayTags!, tag =>
            tag.Contains("older children", StringComparison.OrdinalIgnoreCase));
        Assert.True(childEvidenceResult.ScorePercent > noEvidenceResult.ScorePercent);
    }

    [Fact]
    public async Task AdoptionCopilot_CatQueryDoesNotShowUnrelatedApartmentTags()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var catEvidence = TestDbContextFactory.CreateDog("Cat Evidence Dog");
        catEvidence.Description = "During feeding time she has passed the shelter cats calmly, then returned her attention to the handler.";
        catEvidence.BehaviorDescription = "Gentle handling suits her well.";
        var quietNoCatEvidence = TestDbContextFactory.CreateDog("Quiet No Cat Evidence Dog");
        quietNoCatEvidence.Description = "Enjoys short walks, indoor rest, and quiet evenings indoors.";
        quietNoCatEvidence.BehaviorDescription = "Friendly with familiar people.";
        context.Dogs.AddRange(catEvidence, quietNoCatEvidence);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a cat at home, so I need a dog that may adjust safely to cats");

        var catResult = Assert.Single(response.Results, item => item.DogId == catEvidence.Id);
        var noCatResult = Assert.Single(response.Results, item => item.DogId == quietNoCatEvidence.Id);
        Assert.Contains(catResult.DisplayTags!, tag =>
            tag.Contains("Calm near cats", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(noCatResult.DisplayTags!, tag =>
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Indoor rest", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(noCatResult.DisplayTags!, tag =>
            tag.Contains("Ask shelter about cats", StringComparison.OrdinalIgnoreCase));
        Assert.True(catResult.ScorePercent > noCatResult.ScorePercent);
    }

    [Fact]
    public async Task AdoptionCopilot_AskShelterPrimaryEvidenceCannotBeExcellentEvenWithOpenAiScore()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var noCatEvidence = TestDbContextFactory.CreateDog("No Cat Evidence Dog");
        noCatEvidence.Description = "Enjoys short walks and quiet indoor rest.";
        noCatEvidence.BehaviorDescription = "Friendly with familiar people.";
        context.Dogs.Add(noCatEvidence);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = new OpenAiAdoptionCopilotResponse(true, "Cat matches are ready.",
            [
                new OpenAiAdoptionCopilotItem(noCatEvidence.Id, 1, "Excellent match", 96, ["Ask shelter about cats"], "View details.")
            ])
        };
        fakeCopilotClient.ToolCalls.Add(new OpenAiCopilotToolCall("call_search", "search_dogs", """{"query":"cat friendly dog","count":8}"""));
        var service = CreateCopilotService(context, fakeCopilotClient, EnabledOpenAiSettings());

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a cat at home");

        var result = Assert.Single(response.Results, item => item.DogId == noCatEvidence.Id);
        Assert.Contains(result.DisplayTags!, tag =>
            tag.Contains("Ask shelter about cats", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("Strong match", result.MatchLabel);
        Assert.True(result.ScorePercent <= 80);
    }

    [Fact]
    public async Task AdoptionCopilot_YoungChildrenTreatsOlderChildrenEvidenceAsCautious()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var olderChildrenOnly = TestDbContextFactory.CreateDog("Older Children Only Dog");
        olderChildrenOnly.Description = "Settles near familiar people after a short walk.";
        olderChildrenOnly.BehaviorDescription = "Has done well around older children during supervised visits.";
        var noChildEvidence = TestDbContextFactory.CreateDog("No Child Notes Dog");
        noChildEvidence.Description = "Likes short walks and quiet indoor rest.";
        noChildEvidence.BehaviorDescription = "Friendly with familiar people.";
        context.Dogs.AddRange(olderChildrenOnly, noChildEvidence);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have young children at home");

        var olderChildrenResult = Assert.Single(response.Results, item => item.DogId == olderChildrenOnly.Id);
        var noEvidenceResult = Assert.Single(response.Results, item => item.DogId == noChildEvidence.Id);
        Assert.Contains(olderChildrenResult.DisplayTags!, tag =>
            tag.Contains("older children", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("Strong match", olderChildrenResult.MatchLabel);
        Assert.True(olderChildrenResult.ScorePercent <= 78);
        Assert.Contains(noEvidenceResult.DisplayTags!, tag =>
            tag.Contains("Ask shelter about children", StringComparison.OrdinalIgnoreCase));
        Assert.True(noEvidenceResult.ScorePercent <= 80);
    }

    [Fact]
    public async Task AdoptionCopilot_OlderHouseholdDogDoesNotTreatCalmDogPreferenceAsOverwhelmRisk()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var calmPreferenceDog = TestDbContextFactory.CreateDog("Calm Preference Dog");
        calmPreferenceDog.Description = "Likes quiet indoor rest and short walks.";
        calmPreferenceDog.BehaviorDescription = "Friendly once introduced slowly. He is more comfortable with calm dogs than very energetic ones.";
        var bellaStyleDog = TestDbContextFactory.CreateDog("Bella Style Dog");
        bellaStyleDog.Description = "Enjoys slow walks and a predictable routine.";
        bellaStyleDog.BehaviorDescription = "Gentle and patient during handling. Calm dogs are easier for her than pushy playmates.";
        var liliStyleDog = TestDbContextFactory.CreateDog("Lili Style Dog");
        liliStyleDog.Description = "Enjoys short walks, soft praise, and resting close to people.";
        liliStyleDog.BehaviorDescription = "She takes treats gently and responds well to routine. Pushy dogs can make her retreat, so introductions should stay calm.";
        context.Dogs.AddRange(calmPreferenceDog, bellaStyleDog, liliStyleDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have an older dog at home");

        foreach (var dogId in new[] { calmPreferenceDog.Id, bellaStyleDog.Id, liliStyleDog.Id })
        {
            var result = Assert.Single(response.Results, item => item.DogId == dogId);
            Assert.Contains(result.DisplayTags!, tag =>
                tag.Contains("Calm dog company", StringComparison.OrdinalIgnoreCase) ||
                tag.Contains("Needs slow dog introductions", StringComparison.OrdinalIgnoreCase) ||
                tag.Contains("Not too energetic", StringComparison.OrdinalIgnoreCase) ||
                tag.Contains("Gentle play style", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(result.CautionTags!, tag =>
                tag.Contains("overwhelm", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task AdoptionCopilot_YoungHouseholdDogPrefersPlayfulDogFriends()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var playfulCompanion = TestDbContextFactory.CreateDog("Playful Companion Dog");
        playfulCompanion.Description = "Enjoys training games and outdoor play, then settles after activity.";
        playfulCompanion.BehaviorDescription = "Enjoys sturdy, playful dogs after a proper introduction.";
        var quietOnly = TestDbContextFactory.CreateDog("Quiet Only Dog");
        quietOnly.Description = "Likes quiet indoor rest and short walks.";
        quietOnly.BehaviorDescription = "More comfortable with calm dogs than very energetic ones.";
        context.Dogs.AddRange(playfulCompanion, quietOnly);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a young playful dog at home");

        var playfulResult = Assert.Single(response.Results, result => result.DogId == playfulCompanion.Id);
        var quietResult = Assert.Single(response.Results, result => result.DogId == quietOnly.Id);
        Assert.Equal(playfulCompanion.Id, response.Results.First().DogId);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Compatibility" && constraint.Value.Contains("Young dog", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(playfulResult.DisplayTags!, tag => tag.Contains("Playful dog friends", StringComparison.OrdinalIgnoreCase));
        Assert.True(playfulResult.ScorePercent > quietResult.ScorePercent);
    }

    [Fact]
    public async Task AdoptionCopilot_DoesNotShowUnrequestedFamilyOrExperienceReasons()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var olderChildrenDog = TestDbContextFactory.CreateDog("Older Children Dog");
        olderChildrenDog.Size = DogSize.Medium;
        olderChildrenDog.Description = "Enjoys short daily walks, gentle play, and indoor rest after time with people.";
        olderChildrenDog.BehaviorDescription = "Friendly and attentive around people. Has done well around older children during supervised visits.";
        var experiencedAdopterDog = TestDbContextFactory.CreateDog("Experienced Adopter Dog");
        experiencedAdopterDog.Size = DogSize.Large;
        experiencedAdopterDog.Description = "Enjoys training games, brisk walks, outdoor play, and space to run.";
        experiencedAdopterDog.BehaviorDescription = "Better suited to an experienced adopter who can offer consistent outdoor play.";
        context.Dogs.AddRange(olderChildrenDog, experiencedAdopterDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I want an active dog for a house with a yard");

        var olderChildrenResult = Assert.Single(response.Results, result => result.DogId == olderChildrenDog.Id);
        var experiencedResult = Assert.Single(response.Results, result => result.DogId == experiencedAdopterDog.Id);
        Assert.DoesNotContain(olderChildrenResult.Reasons, reason =>
            reason.Contains("family", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("children", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(experiencedResult.Reasons, reason =>
            reason.Contains("experience", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("experienced", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilotToolSearch_ReturnsOnlyPublicSafeDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Available Dog", DogStatus.Available),
            TestDbContextFactory.CreateDog("Reserved Dog", DogStatus.Reserved),
            TestDbContextFactory.CreateDog("Adopted Dog", DogStatus.Adopted),
            TestDbContextFactory.CreateDog("Treatment Dog", DogStatus.InTreatment));
        await context.SaveChangesAsync();
        var toolService = CreateToolService(context, new OpenAiSettings { Enabled = false });

        var result = await toolService.SearchDogsAsync(
            TestDbContextFactory.AdopterId,
            new AdoptionCopilotSearchDogsArgs { Query = "dog", Count = 10 });

        Assert.Contains(result.Dogs, dog => dog.Dog.Name == "Available Dog");
        Assert.Contains(result.Dogs, dog => dog.Dog.Name == "Reserved Dog");
        Assert.DoesNotContain(result.Dogs, dog => dog.Dog.Name == "Adopted Dog");
        Assert.DoesNotContain(result.Dogs, dog => dog.Dog.Name == "Treatment Dog");
    }

    [Fact]
    public async Task AdoptionCopilotToolSearch_FiltersByMediumSizeAndNeighborhood()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var shelter = await context.Shelters.FindAsync(TestDbContextFactory.ShelterId);
        var otherShelter = await context.Shelters.FindAsync(TestDbContextFactory.OtherShelterId);
        shelter!.Neighborhood = "Zorilor";
        otherShelter!.Neighborhood = "Manastur";
        var mediumZorilor = TestDbContextFactory.CreateDog("Medium Zorilor", shelterId: TestDbContextFactory.ShelterId);
        mediumZorilor.Size = DogSize.Medium;
        var smallZorilor = TestDbContextFactory.CreateDog("Small Zorilor", shelterId: TestDbContextFactory.ShelterId);
        smallZorilor.Size = DogSize.Small;
        var mediumManastur = TestDbContextFactory.CreateDog("Medium Manastur", shelterId: TestDbContextFactory.OtherShelterId);
        mediumManastur.Size = DogSize.Medium;
        context.Dogs.AddRange(mediumZorilor, smallZorilor, mediumManastur);
        await context.SaveChangesAsync();
        var toolService = CreateToolService(context, new OpenAiSettings { Enabled = false });

        var result = await toolService.SearchDogsAsync(
            TestDbContextFactory.AdopterId,
            new AdoptionCopilotSearchDogsArgs
            {
                Query = "medium dog in Zorilor",
                Sizes = ["Medium"],
                Neighborhood = "Zorilor",
                Count = 10
            });

        Assert.Single(result.Dogs);
        Assert.Equal("Medium Zorilor", result.Dogs[0].Dog.Name);
    }

    [Fact]
    public async Task AdoptionCopilotToolSearch_IgnoresZeroMaxAgePlaceholder()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Adult Match", ageYears: 4);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var toolService = CreateToolService(context, new OpenAiSettings { Enabled = false });

        var result = await toolService.SearchDogsAsync(
            TestDbContextFactory.AdopterId,
            new AdoptionCopilotSearchDogsArgs
            {
                Query = "friendly dog",
                MaxAgeYears = 0,
                Count = 10
            });

        Assert.Contains(result.Dogs, candidate => candidate.Dog.Name == "Adult Match");
        Assert.DoesNotContain(result.AppliedConstraints, constraint => constraint.Label == "Age");
    }

    [Fact]
    public async Task AdoptionCopilotToolSearch_EmitsStructuredSensitiveDogEvidenceStrengths()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var directDog = TestDbContextFactory.CreateDog("Direct Evidence Dog");
        directDog.Description = "Enjoys predictable routines.";
        directDog.BehaviorDescription = "Walks politely beside familiar calm dogs and avoids pushy playmates.";
        var indirectDog = TestDbContextFactory.CreateDog("Indirect Evidence Dog");
        indirectDog.Description = "Enjoys short walks, quiet routines, and settling near familiar people.";
        indirectDog.BehaviorDescription = "Gentle with people.";
        var genericDog = TestDbContextFactory.CreateDog("Generic Evidence Dog");
        genericDog.Description = "Friendly dog who likes people.";
        genericDog.BehaviorDescription = "Affectionate with familiar volunteers.";
        context.Dogs.AddRange(directDog, indirectDog, genericDog);
        await context.SaveChangesAsync();
        var toolService = CreateToolService(context, new OpenAiSettings { Enabled = false });

        var result = await toolService.SearchDogsAsync(
            TestDbContextFactory.AdopterId,
            new AdoptionCopilotSearchDogsArgs
            {
                Query = "I have a sick dog recovering at home",
                PrimaryIntent = "Compatibility",
                Compatibility = ["SickDog"],
                CompatibilityTarget = "SensitiveDog",
                ActivityLevel = "Low",
                Count = 10
            });

        var directResult = Assert.Single(result.Dogs, candidate => candidate.DogId == directDog.Id);
        var indirectResult = Assert.Single(result.Dogs, candidate => candidate.DogId == indirectDog.Id);
        var genericResult = Assert.Single(result.Dogs, candidate => candidate.DogId == genericDog.Id);

        Assert.Contains(directResult.PositiveEvidence!, item =>
            item.Label == "Calm dog company" &&
            item.Strength == "Direct" &&
            item.SourceField == "BehaviorDescription" &&
            item.MatchedText!.Contains("calm dogs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indirectResult.PositiveEvidence!, item =>
            item.Label == "Not too energetic" &&
            item.Strength == "Indirect");
        Assert.Contains(indirectResult.MissingEvidence!, item =>
            item.Label.Contains("dog compatibility", StringComparison.OrdinalIgnoreCase) &&
            item.Strength == "Missing");
        Assert.Contains(genericResult.PositiveEvidence!, item =>
            item.Strength == "Generic" &&
            item.Label.Contains("friendly", StringComparison.OrdinalIgnoreCase));
        Assert.True(directResult.ScorePercent > indirectResult.ScorePercent);
        Assert.True(indirectResult.ScorePercent > genericResult.ScorePercent);
    }

    [Fact]
    public async Task AdoptionCopilotToolSearch_IncludesEvidenceItemsInToolDto()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Tool Evidence Dog");
        dog.BehaviorDescription = "Walks politely beside familiar calm dogs.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = OpenAiAdoptionCopilotResponse.Failed("force fallback")
        };
        fakeCopilotClient.ToolCalls.Add(new OpenAiCopilotToolCall(
            "call_search",
            "search_dogs",
            """{"query":"I have a sick dog recovering at home","primaryIntent":"Compatibility","compatibility":["SickDog"],"compatibilityTarget":"SensitiveDog","activityLevel":"Low","count":10}"""));
        var service = CreateCopilotService(context, fakeCopilotClient, EnabledOpenAiSettings());

        await service.AskAsync(TestDbContextFactory.AdopterId, "I have a sick dog recovering at home");

        var output = Assert.Single(fakeCopilotClient.ToolOutputs);
        Assert.Contains("\"positiveEvidence\"", output.OutputJson);
        Assert.Contains("\"strength\":\"Direct\"", output.OutputJson);
        Assert.Contains("\"sourceField\":\"BehaviorDescription\"", output.OutputJson);
    }

    [Fact]
    public async Task AdoptionCopilot_OpenAiRequestDoesNotIncludeSensitiveAdopterFields()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(
            context,
            fullName: "Sensitive Name",
            address: "Secret Street 9",
            phoneNumber: "0700000000",
            additionalNotes: "Do not send this");
        var dog = TestDbContextFactory.CreateDog("Privacy Dog");
        dog.Description = "Friendly dog.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var fakeCopilotClient = new FakeOpenAiAdoptionCopilotClient
        {
            Response = OpenAiAdoptionCopilotResponse.Failed("force fallback")
        };
        var service = CreateCopilotService(context, fakeCopilotClient, settings: EnabledOpenAiSettings());

        await service.AskAsync(TestDbContextFactory.AdopterId, "friendly dog");

        var requestJson = JsonSerializer.Serialize(fakeCopilotClient.LastRequest);
        Assert.DoesNotContain("Sensitive Name", requestJson);
        Assert.DoesNotContain("Secret Street 9", requestJson);
        Assert.DoesNotContain("0700000000", requestJson);
        Assert.DoesNotContain("Do not send this", requestJson);
        Assert.DoesNotContain("FullName", requestJson);
        Assert.DoesNotContain("Address", requestJson);
        Assert.DoesNotContain("PhoneNumber", requestJson);
        Assert.DoesNotContain("AdditionalNotes", requestJson);
    }

    private static ISemanticDogSearchService CreateSemanticService(
        ApplicationDbContext context,
        FakeEmbeddingService? embeddingService = null,
        OpenAiSettings? settings = null)
    {
        embeddingService ??= new FakeEmbeddingService();
        settings ??= EnabledOpenAiSettings();
        return new SemanticDogSearchService(
            context,
            CreateEmbeddingIndex(context, embeddingService, settings),
            new DogSearchDocumentService(),
            embeddingService,
            CreateRecommendationService(context),
            new DistanceService(),
            Options.Create(settings),
            NullLogger<SemanticDogSearchService>.Instance);
    }

    private static DogSearchEmbeddingService CreateEmbeddingIndex(
        ApplicationDbContext context,
        FakeEmbeddingService embeddingService,
        OpenAiSettings? settings = null)
    {
        return new DogSearchEmbeddingService(
            context,
            new DogSearchDocumentService(),
            embeddingService,
            Options.Create(settings ?? EnabledOpenAiSettings()),
            NullLogger<DogSearchEmbeddingService>.Instance);
    }

    private static AdoptionCopilotService CreateCopilotService(
        ApplicationDbContext context,
        FakeOpenAiAdoptionCopilotClient fakeCopilotClient,
        OpenAiSettings? settings = null)
    {
        settings ??= EnabledOpenAiSettings();
        var semanticService = CreateSemanticService(context, settings: settings);
        var toolService = new AdoptionCopilotToolService(
            context,
            semanticService,
            new FakeGeocodingService(),
            new DistanceService());

        return new AdoptionCopilotService(
            context,
            toolService,
            fakeCopilotClient,
            Options.Create(settings),
            NullLogger<AdoptionCopilotService>.Instance);
    }

    private static AdoptionCopilotToolService CreateToolService(ApplicationDbContext context, OpenAiSettings settings)
    {
        return new AdoptionCopilotToolService(
            context,
            CreateSemanticService(context, settings: settings),
            new FakeGeocodingService(),
            new DistanceService());
    }

    private static DogRecommendationService CreateRecommendationService(ApplicationDbContext context)
    {
        return new DogRecommendationService(
            context,
            Options.Create(new OpenAiSettings { Enabled = false }),
            new FakeOpenAiRecommendationClient(),
            NullLogger<DogRecommendationService>.Instance);
    }

    private static OpenAiSettings EnabledOpenAiSettings()
    {
        return new OpenAiSettings
        {
            Enabled = true,
            ApiKey = "test-key",
            ChatModel = "gpt-5.4-mini",
            EmbeddingModel = "text-embedding-3-small"
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
            HasChildren = true,
            HasOtherPets = true,
            ExperienceWithDogs = "Comfortable with dogs.",
            AdditionalNotes = additionalNotes
        });

        context.SaveChanges();
    }

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        public int GenerateCallCount { get; private set; }

        public bool FailGeneration { get; set; }

        public Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            GenerateCallCount++;
            if (FailGeneration)
            {
                return Task.FromResult<float[]?>(null);
            }

            var lower = text.ToLowerInvariant();
            if (lower.Contains("calm") || lower.Contains("apartment"))
            {
                return Task.FromResult<float[]?>([1, 0]);
            }

            if (lower.Contains("active") || lower.Contains("yard"))
            {
                return Task.FromResult<float[]?>([0, 1]);
            }

            return Task.FromResult<float[]?>([0.5f, 0.5f]);
        }

        public double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            if (a.Count == 0 || b.Count == 0 || a.Count != b.Count)
            {
                return 0;
            }

            double dot = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;
            for (var i = 0; i < a.Count; i++)
            {
                dot += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            return magnitudeA == 0 || magnitudeB == 0
                ? 0
                : dot / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        }
    }

    private sealed class FakeOpenAiRecommendationClient : IOpenAiRecommendationClient
    {
        public Task<OpenAiRecommendationResponse> GetEnhancedRecommendationsAsync(
            RecommendationOpenAiRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OpenAiRecommendationResponse.Failed("disabled"));
        }
    }

    private sealed class FakeOpenAiAdoptionCopilotClient : IOpenAiAdoptionCopilotClient
    {
        public AdoptionCopilotToolOpenAiRequest? LastRequest { get; private set; }

        public List<OpenAiCopilotToolCall> ToolCalls { get; } = [];

        public List<OpenAiCopilotToolOutput> ToolOutputs { get; } = [];

        public OpenAiAdoptionCopilotResponse Response { get; set; } =
            OpenAiAdoptionCopilotResponse.Failed("not configured");

        public async Task<OpenAiAdoptionCopilotResponse> AskWithToolsAsync(
            AdoptionCopilotToolOpenAiRequest request,
            OpenAiCopilotToolExecutor executeToolAsync,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            foreach (var toolCall in ToolCalls)
            {
                ToolOutputs.Add(await executeToolAsync(toolCall, cancellationToken));
            }

            return Response;
        }
    }

    private sealed class FakeGeocodingService : IGeocodingService
    {
        public Task<GeocodingResult?> FindCoordinatesAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GeocodingResult?>(new GeocodingResult(46.7712, 23.6236, "Cluj-Napoca"));
        }

        public Task<GeocodingResult?> FindCoordinatesAsync(string address, string city, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GeocodingResult?>(new GeocodingResult(46.7712, 23.6236, "Cluj-Napoca"));
        }

        public Task<ReverseGeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ReverseGeocodingResult?>(null);
        }

        public Task<IReadOnlyList<AddressSuggestion>> SearchAddressSuggestionsAsync(
            string query,
            int limit = 5,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AddressSuggestion>>([]);
        }
    }

    private sealed class TestAdopterProfileService(ApplicationDbContext context) : IAdopterProfileService
    {
        public Task<AdopterProfile?> GetProfileForUserAsync(string userId)
        {
            return Task.FromResult(context.AdopterProfiles.FirstOrDefault(profile => profile.ApplicationUserId == userId));
        }

        public Task CreateOrUpdateProfileAsync(string userId, AdopterProfile profile)
        {
            throw new NotSupportedException();
        }
    }
}
