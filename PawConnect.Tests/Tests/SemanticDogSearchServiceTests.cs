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
    public async Task RebuildIndex_CreatesEmbeddingsForPublicSafeDogsOnly()
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
        Assert.Equal(2, context.DogSearchEmbeddings.Count());
        Assert.All(context.DogSearchEmbeddings.Include(e => e.Dog), embedding =>
            Assert.True(embedding.Dog!.Status is DogStatus.Available or DogStatus.Reserved));
    }

    [Fact]
    public async Task RebuildIndex_MissingApiKeyReturnsSafeFailureWithoutCallingOpenAi()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.Add(TestDbContextFactory.CreateDog("No Key Dog"));
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
        var service = CreateSemanticService(context, embeddingService);

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
        var service = CreateCopilotService(context, fakeCopilotClient);

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

    [Fact]
    public async Task AdoptionCopilot_BlackAndTanFilterReturnsExactCoatColorMatches()
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

        var result = Assert.Single(response.Results, item => item.DogId == blackAndTanDog.Id);
        Assert.Equal("Exact match", result.MatchLabel);
        Assert.Contains("coat color Black and tan", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status filter", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(response.Results, item => item.DogId == goldenDog.Id);
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

        var longerResult = Assert.Single(response.Results, item => item.DogId == longerWalksFit.Id);
        var shortResult = Assert.Single(response.Results, item => item.DogId == shortWalkOnly.Id);
        Assert.Equal(longerWalksFit.Id, response.Results.First().DogId);
        Assert.True(longerResult.ScorePercent > shortResult.ScorePercent);
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Home" && constraint.Value.Contains("Apartment", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Activity" && constraint.Value.Contains("Longer walks", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label == "Temperament" && constraint.Value.Contains("walk", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(longerResult.DisplayTags!, tag =>
            tag.Contains("Longer walks", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(shortResult.DisplayTags!, tag =>
            tag.Contains("Longer walks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdoptionCopilot_ApartmentSupportOutranksSpaceOnlyLongerWalkFit()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var spaceOnlyFit = TestDbContextFactory.CreateDog("Space Only Longer Walk Fit");
        spaceOnlyFit.Size = DogSize.Large;
        spaceOnlyFit.Description = "Enjoys longer walks and open areas with room to run.";
        spaceOnlyFit.BehaviorDescription = "Best reviewed with the shelter for smaller homes.";
        var apartmentSupportedFit = TestDbContextFactory.CreateDog("Apartment Supported Longer Walk Fit");
        apartmentSupportedFit.Size = DogSize.Medium;
        apartmentSupportedFit.Description = "Enjoys longer walks and then settles quickly. His medium size makes daily handling easier.";
        apartmentSupportedFit.BehaviorDescription = "Steady with familiar people.";
        context.Dogs.AddRange(spaceOnlyFit, apartmentSupportedFit);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I live in an apartment but enjoy longer walks");

        var spaceOnlyResult = Assert.Single(response.Results, item => item.DogId == spaceOnlyFit.Id);
        var apartmentSupportedResult = Assert.Single(response.Results, item => item.DogId == apartmentSupportedFit.Id);
        Assert.True(apartmentSupportedResult.ScorePercent > spaceOnlyResult.ScorePercent);
        Assert.Contains(apartmentSupportedResult.DisplayTags!, tag =>
            tag.Contains("Medium size", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apartmentSupportedResult.DisplayTags!, tag =>
            tag.Contains("Settles quickly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(spaceOnlyResult.CautionTags!, tag =>
            tag.Contains("Needs more space", StringComparison.OrdinalIgnoreCase));
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

        var calmResult = Assert.Single(response.Results, item => item.DogId == calmCompanion.Id);
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
            tag.Contains("Short walks", StringComparison.OrdinalIgnoreCase));
        var genericResult = response.Results.FirstOrDefault(item => item.DogId == genericDog.Id);
        if (genericResult is not null)
        {
            Assert.NotEqual("Strong match", genericResult.MatchLabel);
        }
    }

    [Fact]
    public async Task AdoptionCopilot_OlderResidentDogCompatibilityDoesNotFilterForSeniorCandidateAge()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var youngCompatibleDog = TestDbContextFactory.CreateDog("Young Senior Compatible Dog");
        youngCompatibleDog.AgeYears = 2;
        youngCompatibleDog.Age = 2;
        youngCompatibleDog.Description = "He is not pushy with other dogs and prefers gentle, low-pressure interactions.";
        youngCompatibleDog.BehaviorDescription = "A calm first meeting helps him stay respectful around older dogs.";
        var olderQuietDog = TestDbContextFactory.CreateDog("Older Quiet Dog");
        olderQuietDog.AgeYears = 8;
        olderQuietDog.Age = 8;
        olderQuietDog.Description = "She enjoys short walks, soft bedding, and a quiet evening routine.";
        olderQuietDog.BehaviorDescription = "No direct dog-to-dog history is available.";
        context.Dogs.AddRange(youngCompatibleDog, olderQuietDog);
        await context.SaveChangesAsync();
        var service = CreateCopilotService(context, new FakeOpenAiAdoptionCopilotClient(), new OpenAiSettings { Enabled = false });

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I want a dog that will behave around an older dog");

        Assert.Contains(response.AppliedConstraints!, constraint =>
            constraint.Label == "Compatibility" && constraint.Value.Contains("Senior dog", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.AppliedConstraints!, constraint =>
            constraint.Label == "Age");
        var youngResult = Assert.Single(response.Results, result => result.DogId == youngCompatibleDog.Id);
        Assert.Contains(youngResult.DisplayTags!, tag =>
            tag.Contains("Gentle play", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Ask shelter about senior dog fit", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Not too energetic", StringComparison.OrdinalIgnoreCase));

        var toolService = CreateToolService(context, new OpenAiSettings { Enabled = false });
        var toolResult = await toolService.SearchDogsAsync(
            TestDbContextFactory.AdopterId,
            new AdoptionCopilotSearchDogsArgs
            {
                Query = "I want a dog that will behave around an older dog",
                PrimaryIntent = "Compatibility",
                CompatibilityTarget = "SeniorDog",
                Compatibility = ["SeniorDog"],
                MinAgeYears = 7,
                AgeComparison = "AtLeast",
                Count = 10
            });
        Assert.Contains(toolResult.Dogs, result => result.DogId == youngCompatibleDog.Id);
    }

    [Fact]
    public async Task AdoptionCopilot_AskShelterPrimaryEvidenceCannotBeStrongEvenWithOpenAiScore()
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
        var service = CreateCopilotService(context, fakeCopilotClient);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "I have a cat at home");

        var result = Assert.Single(response.Results, item => item.DogId == noCatEvidence.Id);
        Assert.Contains(result.DisplayTags!, tag =>
            tag.Contains("Ask shelter about cats", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("Strong match", result.MatchLabel);
        Assert.True(result.ScorePercent <= 80);
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
            item.Label == "Calm dog company" && item.Strength == "Direct");
        Assert.Contains(indirectResult.PositiveEvidence!, item =>
            item.Label == "Not too energetic" && item.Strength == "Indirect");
        Assert.Contains(indirectResult.MissingEvidence!, item =>
            item.Label.Contains("dog compatibility", StringComparison.OrdinalIgnoreCase) && item.Strength == "Missing");
        Assert.Contains(genericResult.PositiveEvidence!, item =>
            item.Strength == "Generic" && item.Label.Contains("friendly", StringComparison.OrdinalIgnoreCase));
        Assert.True(directResult.ScorePercent > indirectResult.ScorePercent);
        Assert.True(indirectResult.ScorePercent > genericResult.ScorePercent);
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

        public Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            GenerateCallCount++;
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
}
