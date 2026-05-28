using PawConnect.Entities;

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

    private static readonly string[] PlaceholderUrlMarkers =
    [
        "placehold.co",
        "placeholder",
        "no-photo",
        "no_image",
        "no-image",
        "noimage",
        "default-dog",
        "dog-placeholder",
        "pet-placeholder",
        "/images/demo-dogs/",
        "/images/placeholders/"
    ];

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

    public static bool IsValidRealDogImageUrl(string? imageUrl)
    {
        if (!IsValidDisplayImageUrl(imageUrl))
        {
            return false;
        }

        return !IsKnownPlaceholderImageUrl(imageUrl);
    }

    public static List<DogImage> GetRealDogImages(
        IEnumerable<DogImage> images,
        ISet<string>? unavailableImageUrls = null)
    {
        var uniqueImages = new List<DogImage>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var image in images
            .Where(image => IsValidRealDogImageUrl(image.ImageUrl))
            .Where(image => !IsUnavailable(image.ImageUrl, unavailableImageUrls))
            .OrderByDescending(image => image.IsMainImage)
            .ThenBy(image => image.Id))
        {
            var imageUrlKey = NormalizeImageUrlKey(image.ImageUrl);
            if (string.IsNullOrWhiteSpace(imageUrlKey) || !seenUrls.Add(imageUrlKey))
            {
                continue;
            }

            uniqueImages.Add(image);
        }

        return uniqueImages;
    }

    public static string? GetPrimaryRealDogImageUrl(
        IEnumerable<DogImage> images,
        ISet<string>? unavailableImageUrls = null)
    {
        return GetRealDogImages(images, unavailableImageUrls)
            .Select(image => image.ImageUrl)
            .FirstOrDefault();
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

    private static bool IsKnownPlaceholderImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return true;
        }

        var normalizedImageUrl = imageUrl.Trim().Replace('\\', '/');
        return PlaceholderUrlMarkers.Any(marker =>
            normalizedImageUrl.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUnavailable(string? imageUrl, ISet<string>? unavailableImageUrls)
    {
        return !string.IsNullOrWhiteSpace(imageUrl) &&
            unavailableImageUrls?.Contains(imageUrl.Trim()) == true;
    }

    private static string? NormalizeImageUrlKey(string? imageUrl)
    {
        return string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
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
