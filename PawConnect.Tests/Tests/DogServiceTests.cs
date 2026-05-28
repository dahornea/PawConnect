using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class DogServiceTests
{
    [Fact]
    public async Task DeleteDogAsync_DeletesDogWithoutReferences()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Delete Me");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var service = new DogService(context);

        await service.DeleteDogAsync(dog.Id, TestDbContextFactory.ShelterId);

        Assert.False(await context.Dogs.AnyAsync(d => d.Id == dog.Id));
    }

    [Fact]
    public async Task DeleteDogAsync_AllowsDogWithFavoritesAndRemovesFavorites()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Favorite Only");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.FavoriteDogs.Add(new FavoriteDog { DogId = dog.Id, AdopterId = TestDbContextFactory.AdopterId });
        await context.SaveChangesAsync();

        var service = new DogService(context);

        await service.DeleteDogAsync(dog.Id, TestDbContextFactory.ShelterId);

        Assert.False(await context.Dogs.AnyAsync(d => d.Id == dog.Id));
        Assert.False(await context.FavoriteDogs.AnyAsync(f => f.DogId == dog.Id));
    }

    [Fact]
    public async Task DeleteDogAsync_BlocksDogWithAdoptionRequestHistory()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Request History");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.Add(new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            ReasonForAdoption = "I can offer a safe home.",
            Status = AdoptionRequestStatus.Cancelled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new DogService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteDogAsync(dog.Id, TestDbContextFactory.ShelterId));

        Assert.Equal("This dog cannot be deleted because it has adoption request history. To remove it from public listings, change its status instead.", exception.Message);
        Assert.True(await context.Dogs.AnyAsync(d => d.Id == dog.Id));
        Assert.True(await context.AdoptionRequests.AnyAsync(r => r.DogId == dog.Id));
    }

    [Theory]
    [InlineData(0, 7, "7 months old")]
    [InlineData(1, 0, "1 year old")]
    [InlineData(2, 0, "2 years old")]
    [InlineData(1, 3, "1 year, 3 months old")]
    [InlineData(2, 6, "2 years, 6 months old")]
    public void DogAgeFormatter_FormatsYearsAndMonths(int years, int months, string expected)
    {
        var dog = TestDbContextFactory.CreateDog("Age Test", ageYears: years, ageMonths: months);

        Assert.Equal(expected, DogAgeFormatter.Format(dog));
    }

    [Fact]
    public async Task CreateDogAsync_InvalidAgeMonthsFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new DogService(context);
        var dog = TestDbContextFactory.CreateDog("Invalid Puppy", ageYears: 0, ageMonths: 12);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDogAsync(dog, TestDbContextFactory.ShelterId));

        Assert.Equal("Age in months must be between 0 and 11.", exception.Message);
    }

    [Fact]
    public async Task CreateDogAsync_AllowsZeroDailyFoodAmountWhenProvided()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new DogService(context);
        var dog = TestDbContextFactory.CreateDog("Zero Food Amount");
        dog.DailyFoodAmountGrams = 0;

        await service.CreateDogAsync(dog, TestDbContextFactory.ShelterId);

        Assert.Equal(0, (await context.Dogs.SingleAsync(d => d.Name == "Zero Food Amount")).DailyFoodAmountGrams);
    }

    [Fact]
    public async Task GetAvailableDogsAsync_ReturnsOnlyAvailableAndReservedDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Available Dog", DogStatus.Available),
            TestDbContextFactory.CreateDog("Reserved Dog", DogStatus.Reserved),
            TestDbContextFactory.CreateDog("Adopted Dog", DogStatus.Adopted),
            TestDbContextFactory.CreateDog("Treatment Dog", DogStatus.InTreatment));
        await context.SaveChangesAsync();

        var service = new DogService(context);

        var dogs = await service.GetAvailableDogsAsync();

        Assert.Contains(dogs, d => d.Name == "Available Dog");
        Assert.Contains(dogs, d => d.Name == "Reserved Dog");
        Assert.DoesNotContain(dogs, d => d.Name == "Adopted Dog");
        Assert.DoesNotContain(dogs, d => d.Name == "Treatment Dog");
    }

    [Fact]
    public async Task SearchDogsAsync_FiltersPublicDogsByShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Shelter One Dog", DogStatus.Available, TestDbContextFactory.ShelterId),
            TestDbContextFactory.CreateDog("Shelter Two Dog", DogStatus.Available, TestDbContextFactory.OtherShelterId),
            TestDbContextFactory.CreateDog("Shelter One Adopted Dog", DogStatus.Adopted, TestDbContextFactory.ShelterId));
        await context.SaveChangesAsync();

        var service = new DogService(context);

        var dogs = await service.SearchDogsAsync(
            searchTerm: null,
            breed: null,
            maxAge: null,
            size: null,
            location: null,
            status: null,
            sortOption: DogSortOption.NameAsc,
            shelterId: TestDbContextFactory.ShelterId);

        Assert.Contains(dogs, d => d.Name == "Shelter One Dog");
        Assert.DoesNotContain(dogs, d => d.Name == "Shelter Two Dog");
        Assert.DoesNotContain(dogs, d => d.Name == "Shelter One Adopted Dog");
        Assert.All(dogs, dog => Assert.Equal(TestDbContextFactory.ShelterId, dog.ShelterId));
    }

    [Fact]
    public async Task CreateDogAsync_AllowsCustomBreedName()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new DogService(context);
        var dog = new Dog
        {
            Name = "Custom Breed Dog",
            CustomBreedName = "Rare Mountain Dog",
            IsMixedBreed = true,
            AgeYears = 2,
            AgeMonths = 0,
            Size = DogSize.Medium,
            Location = "Cluj-Napoca",
            Status = DogStatus.Available
        };

        await service.CreateDogAsync(dog, TestDbContextFactory.ShelterId);

        var savedDog = await context.Dogs.SingleAsync(item => item.Name == "Custom Breed Dog");
        Assert.Null(savedDog.DogBreedId);
        Assert.Equal("Rare Mountain Dog", savedDog.CustomBreedName);
        Assert.True(savedDog.IsMixedBreed);
        Assert.Equal("Rare Mountain Dog Mix", savedDog.Breed);
    }

    [Fact]
    public async Task SearchDogsAsync_FiltersPublicDogsByShelterNeighborhood()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var testShelter = await context.Shelters.FindAsync(TestDbContextFactory.ShelterId);
        var otherShelter = await context.Shelters.FindAsync(TestDbContextFactory.OtherShelterId);
        testShelter!.Neighborhood = "Zorilor";
        otherShelter!.Neighborhood = "Manastur";
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Zorilor Dog", DogStatus.Available, TestDbContextFactory.ShelterId),
            TestDbContextFactory.CreateDog("Manastur Dog", DogStatus.Available, TestDbContextFactory.OtherShelterId),
            TestDbContextFactory.CreateDog("Adopted Zorilor Dog", DogStatus.Adopted, TestDbContextFactory.ShelterId));
        await context.SaveChangesAsync();

        var service = new DogService(context);

        var dogs = await service.SearchDogsAsync(
            searchTerm: null,
            breed: null,
            maxAge: null,
            size: null,
            location: null,
            status: null,
            sortOption: DogSortOption.NameAsc,
            neighborhood: "zorilor");

        Assert.Contains(dogs, d => d.Name == "Zorilor Dog");
        Assert.DoesNotContain(dogs, d => d.Name == "Manastur Dog");
        Assert.DoesNotContain(dogs, d => d.Name == "Adopted Zorilor Dog");
    }

    [Fact]
    public async Task SearchDogsAsync_FiltersPublicDogsByCoatColor()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var blackDog = TestDbContextFactory.CreateDog("Black Dog");
        blackDog.CoatColor = "Black";
        var goldenDog = TestDbContextFactory.CreateDog("Golden Dog");
        goldenDog.CoatColor = "Golden";
        var adoptedBlackDog = TestDbContextFactory.CreateDog("Adopted Black Dog", DogStatus.Adopted);
        adoptedBlackDog.CoatColor = "Black";
        context.Dogs.AddRange(blackDog, goldenDog, adoptedBlackDog);
        await context.SaveChangesAsync();

        var service = new DogService(context);

        var dogs = await service.SearchDogsAsync(
            searchTerm: null,
            breed: null,
            maxAge: null,
            size: null,
            location: null,
            status: null,
            sortOption: DogSortOption.NameAsc,
            coatColor: "black");

        var dog = Assert.Single(dogs);
        Assert.Equal("Black Dog", dog.Name);
        Assert.Equal("Black", dog.CoatColor);
    }

    [Fact]
    public async Task GetStatusHistoryForDogAsync_ReturnsChangedByUserAndNotes()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("History Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.DogStatusHistories.Add(new DogStatusHistory
        {
            DogId = dog.Id,
            OldStatus = DogStatus.Available,
            NewStatus = DogStatus.Reserved,
            ChangedByUserId = TestDbContextFactory.AdminId,
            Notes = "Admin review note.",
            ChangedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new DogService(context);

        var history = await service.GetStatusHistoryForDogAsync(dog.Id);

        var item = Assert.Single(history);
        Assert.Equal(DogStatus.Available, item.OldStatus);
        Assert.Equal(DogStatus.Reserved, item.NewStatus);
        Assert.Equal("Admin review note.", item.Notes);
        Assert.Equal("admin@test.com", item.ChangedByUser?.Email);
    }
}
