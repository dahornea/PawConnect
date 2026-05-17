namespace PawConnect.Services;

public static class LocalReturnUrlHelper
{
    public static bool IsSafeLocalPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        var trimmed = returnUrl.Trim();
        return trimmed.StartsWith('/') &&
            !trimmed.StartsWith("//", StringComparison.Ordinal) &&
            !trimmed.Contains('\\') &&
            !trimmed.Contains('\r') &&
            !trimmed.Contains('\n');
    }

    public static string GetSafeLocalPath(string? returnUrl, string fallback)
    {
        return IsSafeLocalPath(returnUrl) ? returnUrl!.Trim() : fallback;
    }
}
