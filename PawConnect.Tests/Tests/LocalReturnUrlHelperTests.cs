using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class LocalReturnUrlHelperTests
{
    [Theory]
    [InlineData("/dogs")]
    [InlineData("/admin/dogs")]
    [InlineData("/adopter/copilot?tab=last")]
    public void IsSafeLocalPath_AllowsLocalAppPaths(string returnUrl)
    {
        Assert.True(LocalReturnUrlHelper.IsSafeLocalPath(returnUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("/admin\\dogs")]
    [InlineData("/admin/dogs\r\nLocation: https://example.com")]
    public void IsSafeLocalPath_BlocksExternalOrMalformedValues(string returnUrl)
    {
        Assert.False(LocalReturnUrlHelper.IsSafeLocalPath(returnUrl));
    }

    [Fact]
    public void GetSafeLocalPath_ReturnsFallbackForUnsafeUrl()
    {
        Assert.Equal("/dogs", LocalReturnUrlHelper.GetSafeLocalPath("https://example.com", "/dogs"));
    }
}
