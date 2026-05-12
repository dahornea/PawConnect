using System.Net;
using Microsoft.AspNetCore.Identity;
using PawConnect.Data;

namespace PawConnect.Services;

public class PawConnectIdentityEmailSender(IEmailService emailService, ILogger<PawConnectIdentityEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var link = WebUtility.HtmlDecode(confirmationLink);
        var displayName = GetDisplayName(user, email);
        var body = $"""
            Hello {displayName},

            Please confirm your PawConnect account by opening this link:

            {link}

            If you did not create a PawConnect account, you can ignore this email.
            """;

        var htmlBody = PawConnectEmailTemplate.BuildHtml(
            "Confirm your PawConnect account",
            $"Hello {displayName},",
            ["Please confirm your PawConnect account."],
            new EmailTemplateButton("Confirm Account", link),
            link,
            note: "If you did not create a PawConnect account, you can ignore this email.");

        return SendIdentityEmailAsync(email, "Confirm your PawConnect account", body, htmlBody);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var link = WebUtility.HtmlDecode(resetLink);
        var displayName = GetDisplayName(user, email);
        var body = $"""
            Hello {displayName},

            We received a request to reset your PawConnect password.

            Open the link below to reset your password. If it is not clickable, copy and paste it into your browser:

            {link}

            If you did not request a password reset, you can ignore this email.
            """;

        var htmlBody = PawConnectEmailTemplate.BuildHtml(
            "Reset your PawConnect password",
            $"Hello {displayName},",
            ["We received a request to reset your PawConnect password."],
            new EmailTemplateButton("Reset Password", link),
            link,
            note: "If you did not request a password reset, you can ignore this email.");

        return SendIdentityEmailAsync(email, "Reset your PawConnect password", body, htmlBody);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var body = $"""
            Hello {GetDisplayName(user, email)},

            We received a request to reset your PawConnect password.

            Use this reset code:
            {resetCode}

            If you did not request a password reset, you can ignore this email.
            """;

        return SendIdentityEmailAsync(email, "Reset your PawConnect password", body);
    }

    private async Task SendIdentityEmailAsync(string email, string subject, string body, string? htmlBody = null)
    {
        try
        {
            await emailService.SendEmailAsync(email, subject, body, htmlBody: htmlBody);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Identity email could not be sent to {Recipient}. Subject: {Subject}", email, subject);
        }
    }

    private static string GetDisplayName(ApplicationUser user, string email)
    {
        return string.IsNullOrWhiteSpace(user.FullName) ? email : user.FullName.Trim();
    }

}
