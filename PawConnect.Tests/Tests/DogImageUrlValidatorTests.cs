using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class DogImageUrlValidatorTests
{
    [Fact]
    public void IsValidDisplayImageUrl_AcceptsDirectImageUrl()
    {
        Assert.True(DogImageUrlValidator.IsValidDisplayImageUrl("https://example.com/dog.jpg?size=large"));
    }

    [Fact]
    public void IsValidDisplayImageUrl_RejectsInvalidOrPlaceholderUrl()
    {
        Assert.False(DogImageUrlValidator.IsValidDisplayImageUrl("https://placehold.co/800x500?text=Dog"));
        Assert.False(DogImageUrlValidator.IsValidDisplayImageUrl("not-a-url"));
    }

    [Fact]
    public void TryNormalize_TrimsValidImageUrl()
    {
        var isValid = DogImageUrlValidator.TryNormalize(" https://example.com/dog.jpg ", out var normalized);

        Assert.True(isValid);
        Assert.Equal("https://example.com/dog.jpg", normalized);
    }

    [Fact]
    public void IsValidRealDogImageUrl_RejectsPlaceholderAndSvgImages()
    {
        Assert.False(DogImageUrlValidator.IsValidRealDogImageUrl("/images/dog-placeholder.svg"));
        Assert.False(DogImageUrlValidator.IsValidRealDogImageUrl("https://example.com/dog.svg"));
    }

    [Fact]
    public void GetPrimaryRealDogImageUrl_PrefersMainRealImage()
    {
        var images = new[]
        {
            new DogImage { Id = 1, ImageUrl = "https://example.com/first.jpg" },
            new DogImage { Id = 2, ImageUrl = "https://example.com/main.jpg", IsMainImage = true },
            new DogImage { Id = 3, ImageUrl = "https://example.com/other.jpg" }
        };

        var imageUrl = DogImageUrlValidator.GetPrimaryRealDogImageUrl(images);

        Assert.Equal("https://example.com/main.jpg", imageUrl);
    }

    [Fact]
    public void GetPrimaryRealDogImageUrl_FallsBackWhenMainImageIsPlaceholder()
    {
        var images = new[]
        {
            new DogImage { Id = 1, ImageUrl = "/images/dog-placeholder.svg", IsMainImage = true },
            new DogImage { Id = 2, ImageUrl = "https://example.com/real.jpg" }
        };

        var imageUrl = DogImageUrlValidator.GetPrimaryRealDogImageUrl(images);

        Assert.Equal("https://example.com/real.jpg", imageUrl);
    }

    [Fact]
    public void GetRealDogImages_ExcludesInvalidUnavailablePlaceholderAndDuplicateImages()
    {
        var unavailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "https://example.com/broken.jpg"
        };
        var images = new[]
        {
            new DogImage { Id = 1, ImageUrl = "https://example.com/broken.jpg", IsMainImage = true },
            new DogImage { Id = 2, ImageUrl = "https://placehold.co/800x500?text=Dog" },
            new DogImage { Id = 3, ImageUrl = "https://example.com/real.jpg" },
            new DogImage { Id = 4, ImageUrl = " https://example.com/real.jpg " },
            new DogImage { Id = 5, ImageUrl = "not-a-url" }
        };

        var realImages = DogImageUrlValidator.GetRealDogImages(images, unavailable);

        var image = Assert.Single(realImages);
        Assert.Equal("https://example.com/real.jpg", image.ImageUrl);
    }
}
