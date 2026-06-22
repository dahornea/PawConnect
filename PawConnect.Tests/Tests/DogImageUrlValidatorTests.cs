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

    [Theory]
    [InlineData("https://www.borrowmydoggy.com/_next/image?url=https%3A%2F%2Fcdn.sanity.io%2Fimages%2F4ij0poqn%2Fproduction%2Fe24bfbd855cda99e303975f2bd2a1bf43079b320-800x600.jpg&w=1080&q=80")]
    [InlineData("https://cms.paw-champ.com/api/assets/dog-wiki/5e6f4c0b-d87f-48c8-94b3-318508a1316e?cache=3600")]
    public void IsValidDisplayImageUrl_AcceptsSupportedImageProxyUrls(string imageUrl)
    {
        Assert.True(DogImageUrlValidator.IsValidDisplayImageUrl(imageUrl));
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
