using PawConnect.Services;

namespace PawConnect.Services.Caching;

public static class CacheKeys
{
    public const string ActiveDogBreeds = "lookup:dog-breeds:active";
    public const string ResourceCategoriesAll = "lookup:resource-categories:all";
    public const string FoodTypesAll = "lookup:food-types:all";
    public const string AnalyticsPrefix = "analytics:";
    public const string AdminShelterOptions = "analytics:admin:shelter-options";

    public static string AdminAnalytics(AnalyticsDateRange range, int? shelterId)
    {
        return Scoped(
            "analytics:admin",
            shelterId.HasValue ? $"shelter:{shelterId.Value}" : "platform",
            range.StartUtc.Ticks,
            range.EndUtc.Ticks);
    }

    public static string ShelterAnalytics(int shelterId, AnalyticsDateRange range)
    {
        return Scoped(
            "analytics:shelter",
            $"shelter:{shelterId}",
            range.StartUtc.Ticks,
            range.EndUtc.Ticks);
    }

    public static string Scoped(string area, string scope, params object?[] parts)
    {
        var normalizedParts = parts
            .Where(part => part is not null)
            .Select(part => part!.ToString())
            .Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join(":", [area, scope, .. normalizedParts]);
    }
}
