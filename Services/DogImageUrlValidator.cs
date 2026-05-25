namespace PawConnect.Services;

public static class DogImageUrlValidator
{
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };

    public const string ValidationMessage = "Please enter a valid image URL starting with http:// or https://.";

    public static bool IsValidDisplayImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return false;
        }

        var trimmedImageUrl = imageUrl.Trim();
        if (trimmedImageUrl.Contains("placehold.co", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsLocalDisplayImageUrl(trimmedImageUrl))
        {
            return true;
        }

        return TryNormalize(trimmedImageUrl, out _);
    }

    public static bool TryNormalize(string? imageUrl, out string normalizedImageUrl)
    {
        normalizedImageUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return false;
        }

        normalizedImageUrl = imageUrl.Trim();
        if (normalizedImageUrl.Contains("placehold.co", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(normalizedImageUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var extension = Path.GetExtension(uri.AbsolutePath);
        return !string.IsNullOrWhiteSpace(extension) && AllowedImageExtensions.Contains(extension);
    }

    private static bool IsLocalDisplayImageUrl(string imageUrl)
    {
        if (!imageUrl.StartsWith("/", StringComparison.Ordinal) ||
            imageUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var path = imageUrl.Split('?', '#')[0];
        var extension = Path.GetExtension(path);
        return AllowedImageExtensions.Contains(extension) ||
            extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }
}
