using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class SavedDogSearchServiceTests
{
    [Fact]
    public async Task CreateSavedSearchAsync_PersistsCriteriaAndInitialMatches()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Apartment Buddy");
        dog.Size = DogSize.Small;
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var saved = await service.CreateSavedSearchAsync(
            TestDbContextFactory.AdopterId,
            new SavedDogSearchCreateRequest(
                "Small dogs",
                new SavedDogSearchCriteriaDto(Size: DogSize.Small)));

        Assert.Equal("Small dogs", saved.Name);
        Assert.Equal(1, saved.TotalMatches);
        Assert.Equal(1, saved.NewMatches);
        Assert.True(await context.SavedSearchMatches.AnyAsync(match => match.DogId == dog.Id));
    }

    [Fact]
    public async Task CreateSavedSearchAsync_BlocksNonAdopterUsers()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSavedSearchAsync(
                TestDbContextFactory.ShelterUserId,
                new SavedDogSearchCreateRequest("Medium dogs", new SavedDogSearchCriteriaDto(Size: DogSize.Medium))));

        Assert.Equal("Only adopter accounts can manage saved searches.", exception.Message);
    }

    [Fact]
    public async Task CreateSavedSearchAsync_BlocksDuplicateNamesForSameAdopter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);
        var request = new SavedDogSearchCreateRequest("Medium dogs", new SavedDogSearchCriteriaDto(Size: DogSize.Medium));

        await service.CreateSavedSearchAsync(TestDbContextFactory.AdopterId, request);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSavedSearchAsync(TestDbContextFactory.AdopterId, request));
        Assert.Equal("You already have a saved search with this name.", exception.Message);
    }

    [Fact]
    public async Task EvaluateSavedSearchAsync_RemovesDogsThatAreNoLongerPublicSafe()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Visible Match");
        dog.Size = DogSize.Medium;
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var saved = await service.CreateSavedSearchAsync(
            TestDbContextFactory.AdopterId,
            new SavedDogSearchCreateRequest("Medium dogs", new SavedDogSearchCriteriaDto(Size: DogSize.Medium)));

        dog.Status = DogStatus.Adopted;
        await context.SaveChangesAsync();

        var details = await service.EvaluateSavedSearchAsync(saved.Id, TestDbContextFactory.AdopterId);

        Assert.NotNull(details);
        Assert.Empty(details.Matches);
        Assert.Contains(await context.SavedSearchMatches.ToListAsync(), match => match.Status == SavedSearchMatchStatus.NoLongerMatching);
    }

    [Fact]
    public async Task DismissMatchAsync_BlocksAnotherAdopter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Private Match");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        await service.CreateSavedSearchAsync(
            TestDbContextFactory.AdopterId,
            new SavedDogSearchCreateRequest("Medium dogs", new SavedDogSearchCriteriaDto(Size: DogSize.Medium)));
        var matchId = await context.SavedSearchMatches.Select(match => match.Id).SingleAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DismissMatchAsync(matchId, TestDbContextFactory.SecondAdopterId));

        Assert.Equal("Saved search match was not found.", exception.Message);
    }

    [Fact]
    public async Task MarkMatchAsSeenAsync_UpdatesOnlyOwnedNewMatch()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Seen Match");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        await service.CreateSavedSearchAsync(
            TestDbContextFactory.AdopterId,
            new SavedDogSearchCreateRequest("Medium dogs", new SavedDogSearchCriteriaDto(Size: DogSize.Medium)));
        var matchId = await context.SavedSearchMatches.Select(match => match.Id).SingleAsync();

        await service.MarkMatchAsSeenAsync(matchId, TestDbContextFactory.AdopterId);

        var match = await context.SavedSearchMatches.SingleAsync(match => match.Id == matchId);
        Assert.Equal(SavedSearchMatchStatus.Seen, match.Status);
        Assert.NotNull(match.SeenAtUtc);
    }

    private static SavedDogSearchService CreateService(ApplicationDbContext context)
    {
        return new SavedDogSearchService(context, new DistanceService());
    }
}
