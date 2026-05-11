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
}
