using PawConnect.Services;
using PawConnect.Entities;

namespace PawConnect.Tests.Tests;

public class DogImageUrlValidatorTests
{
    [Theory]
    [InlineData("https://example.com/dog.jpg")]
    [InlineData("http://example.com/dog.jpeg")]
    [InlineData("https://example.com/dog.png?size=large")]
    [InlineData("https://example.com/path/dog.webp#preview")]
    [InlineData("https://example.com/dog.gif")]
    [InlineData("/images/demo-dogs/dog-small.svg")]
    [InlineData("/images/demo-dogs/dog-small.jpg")]
    public void IsValidDisplayImageUrl_ReturnsTrueForValidDirectImageUrls(string imageUrl)
    {
        Assert.True(DogImageUrlValidator.IsValidDisplayImageUrl(imageUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("images/dog.jpg")]
    [InlineData("//example.com/dog.jpg")]
    [InlineData("ftp://example.com/dog.jpg")]
    [InlineData("https://example.com/dog")]
    [InlineData("https://example.com/dog.svg")]
    [InlineData("https://placehold.co/800x500?text=Dog")]
    public void IsValidDisplayImageUrl_ReturnsFalseForInvalidOrPlaceholderUrls(string imageUrl)
    {
        Assert.False(DogImageUrlValidator.IsValidDisplayImageUrl(imageUrl));
    }

    [Fact]
    public void TryNormalize_TrimsValidImageUrl()
    {
        var isValid = DogImageUrlValidator.TryNormalize(" https://example.com/dog.jpg ", out var normalized);

        Assert.True(isValid);
        Assert.Equal("https://example.com/dog.jpg", normalized);
    }

    [Theory]
    [InlineData("https://example.com/dog.jpg")]
    [InlineData("https://www.thelabradorsite.com/wp-content/uploads/2023/06/sheprador-buddy.jpg")]
    [InlineData("https://cdn.britannica.com/80/232780-050-404D6708/Pembroke-welsh-corgi-dog.jpg")]
    [InlineData("https://jesypet.ro/wp-content/uploads/2025/08/Bichon-Maltez-%E2%80%93-Ghid-complet-despre-ca%CC%82inele-mic-cu-inima%CC%86-mare.webp")]
    [InlineData("https://img.fera.ro/images/companies/1/seter-szkocki-fci.png?1704310402663")]
    [InlineData("https://www.purina.com/sites/default/files/styles/social_share/public/2025-09/siberian_husky_4_1.jpg?h=f7d9296c&itok=medyY_xK")]
    public void IsValidRealDogImageUrl_ReturnsTrueForRealDogImages(string imageUrl)
    {
        Assert.True(DogImageUrlValidator.IsValidRealDogImageUrl(imageUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("https://placehold.co/800x500?text=Dog")]
    [InlineData("/images/placeholders/dog.svg")]
    [InlineData("/images/dog-placeholder.svg")]
    [InlineData("/images/default-dog.svg")]
    [InlineData("/images/demo-dogs/labrador-retriever-mix.svg")]
    public void IsValidRealDogImageUrl_ReturnsFalseForInvalidOrPlaceholderImages(string imageUrl)
    {
        Assert.False(DogImageUrlValidator.IsValidRealDogImageUrl(imageUrl));
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
    public void GetRealDogImages_ExcludesInvalidUnavailableAndPlaceholderImages()
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
            new DogImage { Id = 4, ImageUrl = "not-a-url" }
        };

        var realImages = DogImageUrlValidator.GetRealDogImages(images, unavailable);

        var image = Assert.Single(realImages);
        Assert.Equal("https://example.com/real.jpg", image.ImageUrl);
    }

    [Fact]
    public void GetRealDogImages_DeduplicatesRepeatedRealImageUrls()
    {
        var images = new[]
        {
            new DogImage { Id = 1, ImageUrl = "https://example.com/dog.jpg", IsMainImage = true },
            new DogImage { Id = 2, ImageUrl = " https://example.com/dog.jpg " },
            new DogImage { Id = 3, ImageUrl = "https://example.com/other.jpg" }
        };

        var realImages = DogImageUrlValidator.GetRealDogImages(images);

        Assert.Collection(
            realImages,
            image => Assert.Equal("https://example.com/dog.jpg", image.ImageUrl),
            image => Assert.Equal("https://example.com/other.jpg", image.ImageUrl));
    }
}
