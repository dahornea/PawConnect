using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class DogImageServiceTests
{
    [Fact]
    public async Task AddDogImageAsync_AddsImageForShelterDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Image Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = new DogImageService(context);

        await service.AddDogImageAsync(dog.Id, TestDbContextFactory.ShelterId, new DogImage
        {
            ImageUrl = "https://example.com/dog.jpg",
            IsMainImage = true
        });

        var image = await context.DogImages.SingleAsync(item => item.DogId == dog.Id);
        Assert.Equal("https://example.com/dog.jpg", image.ImageUrl);
        Assert.True(image.IsMainImage);
    }

    [Fact]
    public async Task AddDogImageAsync_RejectsEmptyImageUrl()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Empty Image Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = new DogImageService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddDogImageAsync(dog.Id, TestDbContextFactory.ShelterId, new DogImage { ImageUrl = "   " }));

        Assert.Equal("Image URL is required.", exception.Message);
        Assert.False(await context.DogImages.AnyAsync(image => image.DogId == dog.Id));
    }

    [Fact]
    public async Task AddDogImageAsync_RejectsInvalidImageUrl()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Invalid Image Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = new DogImageService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddDogImageAsync(dog.Id, TestDbContextFactory.ShelterId, new DogImage { ImageUrl = "https://placehold.co/800x500?text=Dog" }));

        Assert.Equal(DogImageUrlValidator.ValidationMessage, exception.Message);
        Assert.False(await context.DogImages.AnyAsync(image => image.DogId == dog.Id));
    }

    [Fact]
    public async Task AddDogImageAsync_BlocksDuplicateImageUrlForSameDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Duplicate Image Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.DogImages.Add(new DogImage { DogId = dog.Id, ImageUrl = "https://example.com/dog.jpg" });
        await context.SaveChangesAsync();
        var service = new DogImageService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddDogImageAsync(dog.Id, TestDbContextFactory.ShelterId, new DogImage { ImageUrl = " https://example.com/dog.jpg " }));

        Assert.Equal("This image has already been added for this dog.", exception.Message);
        Assert.Equal(1, await context.DogImages.CountAsync(image => image.DogId == dog.Id));
    }

    [Fact]
    public async Task CreateDogWithoutImages_StillSucceeds()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("No Image Dog");
        var service = new DogService(context);

        await service.CreateDogAsync(dog, TestDbContextFactory.ShelterId);

        Assert.True(await context.Dogs.AnyAsync(item => item.Name == "No Image Dog"));
        Assert.False(await context.DogImages.AnyAsync());
    }

    [Fact]
    public async Task DeleteDogImageAsync_BlocksOtherShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Protected Image Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var image = new DogImage { DogId = dog.Id, ImageUrl = "https://example.com/dog.jpg" };
        context.DogImages.Add(image);
        await context.SaveChangesAsync();
        var service = new DogImageService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteDogImageAsync(image.Id, TestDbContextFactory.OtherShelterId));

        Assert.True(await context.DogImages.AnyAsync(item => item.Id == image.Id));
    }
}
