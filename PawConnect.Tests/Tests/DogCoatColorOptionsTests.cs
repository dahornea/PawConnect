using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class DogCoatColorOptionsTests
{
    [Theory]
    [InlineData("black dog", "Black")]
    [InlineData("black dog", "Black and tan")]
    [InlineData("white small dog", "White")]
    [InlineData("white small dog", "Brown and white")]
    [InlineData("brown Labrador", "Brown")]
    [InlineData("brown Labrador", "Brown and white")]
    [InlineData("black and tan shepherd", "Black and tan")]
    [InlineData("tri-color corgi", "Tricolor")]
    public void DetectInText_ReturnsKnownCoatColor(string query, string expected)
    {
        var colors = DogCoatColorOptions.DetectInText(query);

        Assert.Contains(expected, colors);
    }

    [Fact]
    public void DetectInText_KeepsExplicitCompoundColorSpecific()
    {
        var colors = DogCoatColorOptions.DetectInText("black and tan shepherd");

        var color = Assert.Single(colors);
        Assert.Equal("Black and tan", color);
    }
}
