using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class FavoriteDogServiceTests
{
    [Fact]
    public async Task AddFavoriteAsync_AddsFavoriteForAdopter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Favorite Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.AddFavoriteAsync(TestDbContextFactory.AdopterId, dog.Id);

        Assert.True(await context.FavoriteDogs.AnyAsync(f =>
            f.AdopterId == TestDbContextFactory.AdopterId && f.DogId == dog.Id));
    }

    [Fact]
    public async Task AddFavoriteAsync_DoesNotCreateDuplicateFavorites()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Duplicate Favorite Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.AddFavoriteAsync(TestDbContextFactory.AdopterId, dog.Id);
        await service.AddFavoriteAsync(TestDbContextFactory.AdopterId, dog.Id);

        Assert.Equal(1, await context.FavoriteDogs.CountAsync(f => f.AdopterId == TestDbContextFactory.AdopterId && f.DogId == dog.Id));
    }

    [Fact]
    public async Task RemoveFavoriteAsync_OnlyRemovesCurrentAdoptersFavorite()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Shared Favorite Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.FavoriteDogs.AddRange(
            new FavoriteDog { DogId = dog.Id, AdopterId = TestDbContextFactory.AdopterId },
            new FavoriteDog { DogId = dog.Id, AdopterId = TestDbContextFactory.SecondAdopterId });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.RemoveFavoriteAsync(TestDbContextFactory.AdopterId, dog.Id);

        Assert.False(await context.FavoriteDogs.AnyAsync(f => f.AdopterId == TestDbContextFactory.AdopterId && f.DogId == dog.Id));
        Assert.True(await context.FavoriteDogs.AnyAsync(f => f.AdopterId == TestDbContextFactory.SecondAdopterId && f.DogId == dog.Id));
    }

    [Theory]
    [InlineData(TestDbContextFactory.ShelterUserId)]
    [InlineData(TestDbContextFactory.AdminId)]
    public async Task AddFavoriteAsync_BlocksNonAdopterUsers(string userId)
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Protected Favorite Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddFavoriteAsync(userId, dog.Id));

        Assert.Equal("Only adopter accounts can manage favorite dogs.", exception.Message);
    }

    private static FavoriteDogService CreateService(ApplicationDbContext context)
    {
        return new FavoriteDogService(context, TestDbContextFactory.CreateUserManager(context));
    }
}
