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

    [Theory]
    [InlineData("https://example.com/dog.jpg")]
    [InlineData("https://example.com/dog.jpeg")]
    [InlineData("https://example.com/dog.png")]
    [InlineData("https://example.com/dog.webp")]
    [InlineData("https://example.com/dog.gif")]
    [InlineData("https://example.com/dog.jpg?width=800")]
    public async Task AddDogImageAsync_AcceptsValidDirectImageUrls(string imageUrl)
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog($"Valid Image Dog {Guid.NewGuid():N}");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var service = new DogImageService(context);

        await service.AddDogImageAsync(dog.Id, TestDbContextFactory.ShelterId, new DogImage
        {
            ImageUrl = imageUrl
        });

        Assert.True(await context.DogImages.AnyAsync(image => image.DogId == dog.Id && image.ImageUrl == imageUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddDogImageAsync_RejectsEmptyImageUrls(string imageUrl)
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Empty Image Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var service = new DogImageService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddDogImageAsync(dog.Id, TestDbContextFactory.ShelterId, new DogImage
            {
                ImageUrl = imageUrl
            }));

        Assert.Equal("Image URL is required.", exception.Message);
        Assert.False(await context.DogImages.AnyAsync(image => image.DogId == dog.Id));
    }

    [Theory]
    [InlineData("/images/dog.jpg")]
    [InlineData("ftp://example.com/dog.jpg")]
    [InlineData("https://example.com/dog")]
    [InlineData("https://example.com/dog.txt")]
    [InlineData("https://placehold.co/800x500?text=Dog")]
    public async Task AddDogImageAsync_RejectsInvalidImageUrls(string imageUrl)
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Invalid Image Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var service = new DogImageService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddDogImageAsync(dog.Id, TestDbContextFactory.ShelterId, new DogImage
            {
                ImageUrl = imageUrl
            }));

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
