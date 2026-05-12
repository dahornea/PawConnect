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

        var image = await context.DogImages.SingleAsync(i => i.DogId == dog.Id);
        Assert.Equal("https://example.com/dog.jpg", image.ImageUrl);
        Assert.True(image.IsMainImage);
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
            service.AddDogImageAsync(dog.Id, TestDbContextFactory.ShelterId, new DogImage
            {
                ImageUrl = " https://example.com/dog.jpg "
            }));

        Assert.Equal("This image has already been added for this dog.", exception.Message);
        Assert.Equal(1, await context.DogImages.CountAsync(i => i.DogId == dog.Id));
    }

    [Fact]
    public async Task AddDogImageAsync_AllowsSameImageUrlForDifferentDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Image Dog A");
        var otherDog = TestDbContextFactory.CreateDog("Image Dog B");
        context.Dogs.AddRange(dog, otherDog);
        await context.SaveChangesAsync();
        context.DogImages.Add(new DogImage { DogId = dog.Id, ImageUrl = "https://example.com/shared.jpg" });
        await context.SaveChangesAsync();

        var service = new DogImageService(context);

        await service.AddDogImageAsync(otherDog.Id, TestDbContextFactory.ShelterId, new DogImage
        {
            ImageUrl = "https://example.com/shared.jpg"
        });

        Assert.Equal(2, await context.DogImages.CountAsync(i => i.ImageUrl == "https://example.com/shared.jpg"));
    }

    [Fact]
    public async Task CreateDogWithoutImages_StillSucceeds()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("No Image Dog");
        var service = new DogService(context);

        await service.CreateDogAsync(dog, TestDbContextFactory.ShelterId);

        Assert.True(await context.Dogs.AnyAsync(d => d.Name == "No Image Dog"));
        Assert.False(await context.DogImages.AnyAsync());
    }

    [Fact]
    public async Task DeleteDogImageAsync_RemovesImageForOwnerShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Delete Image Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var image = new DogImage { DogId = dog.Id, ImageUrl = "https://example.com/dog.jpg" };
        context.DogImages.Add(image);
        await context.SaveChangesAsync();

        var service = new DogImageService(context);

        await service.DeleteDogImageAsync(image.Id, TestDbContextFactory.ShelterId);

        Assert.False(await context.DogImages.AnyAsync(i => i.Id == image.Id));
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

        Assert.True(await context.DogImages.AnyAsync(i => i.Id == image.Id));
    }
}
