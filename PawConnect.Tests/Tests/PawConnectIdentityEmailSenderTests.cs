using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class PawConnectIdentityEmailSenderTests
{
    [Fact]
    public async Task SendPasswordResetLinkAsync_UsesConfiguredEmailService()
    {
        var emailService = new TestEmailService();
        var sender = new PawConnectIdentityEmailSender(emailService, NullLogger<PawConnectIdentityEmailSender>.Instance);
        var user = new ApplicationUser { FullName = "Demo Adopter" };

        await sender.SendPasswordResetLinkAsync(user, "adopter@test.com", "https://localhost/Account/ResetPassword?code=abc123");

        var email = Assert.Single(emailService.SentEmails);
        Assert.Equal("adopter@test.com", email.To);
        Assert.Equal("Reset your PawConnect password", email.Subject);
        Assert.Contains("Demo Adopter", email.Body);
        Assert.Contains("Open the link below to reset your password. If it is not clickable, copy and paste it into your browser:", email.Body);
        Assert.Contains("https://localhost/Account/ResetPassword?code=abc123", email.Body);
        Assert.Contains("If you did not request a password reset", email.Body);
        Assert.NotNull(email.HtmlBody);
        Assert.Contains("Reset Password", email.HtmlBody);
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_DecodesIdentityEncodedLinks()
    {
        var emailService = new TestEmailService();
        var sender = new PawConnectIdentityEmailSender(emailService, NullLogger<PawConnectIdentityEmailSender>.Instance);
        var user = new ApplicationUser();

        await sender.SendPasswordResetLinkAsync(user, "adopter@test.com", "https://localhost/Account/ResetPassword?code=abc&amp;returnUrl=%2Fdogs");

        var email = Assert.Single(emailService.SentEmails);
        Assert.Contains("code=abc&returnUrl=%2Fdogs", email.Body);
        Assert.Contains("code=abc&amp;returnUrl=%2Fdogs", email.HtmlBody);
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_UsesConfiguredEmailService()
    {
        var emailService = new TestEmailService();
        var sender = new PawConnectIdentityEmailSender(emailService, NullLogger<PawConnectIdentityEmailSender>.Instance);
        var user = new ApplicationUser { FullName = "Demo User" };

        await sender.SendConfirmationLinkAsync(user, "user@test.com", "https://localhost/Account/ConfirmEmail?code=abc");

        var email = Assert.Single(emailService.SentEmails);
        Assert.Equal("user@test.com", email.To);
        Assert.Equal("Confirm your PawConnect account", email.Subject);
        Assert.Contains("Demo User", email.Body);
        Assert.Contains("https://localhost/Account/ConfirmEmail?code=abc", email.Body);
    }
}
