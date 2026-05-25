using PawConnect.Services;

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
}
