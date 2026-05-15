using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PawConnect.Services;

public class NominatimGeocodingService(HttpClient httpClient, ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    private readonly Dictionary<string, IReadOnlyList<AddressSuggestion>> _suggestionCache = new(StringComparer.OrdinalIgnoreCase);

    public Task<GeocodingResult?> FindCoordinatesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<GeocodingResult?>(null);
        }

        return FindCoordinatesForQueryAsync($"{query.Trim()}, Romania", cancellationToken);
    }

    public async Task<IReadOnlyList<AddressSuggestion>> SearchAddressSuggestionsAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 3)
        {
            return [];
        }

        var normalizedLimit = Math.Clamp(limit, 1, 5);
        var cacheKey = $"{normalizedQuery}|{normalizedLimit}";
        if (_suggestionCache.TryGetValue(cacheKey, out var cachedSuggestions))
        {
            return cachedSuggestions;
        }

        var encodedQuery = Uri.EscapeDataString($"{normalizedQuery}, Romania");
        var requestUri = $"search?format=json&limit={normalizedLimit}&addressdetails=1&countrycodes=ro&accept-language=ro,en&q={encodedQuery}";

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nominatim address suggestion search failed with status {StatusCode}.", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var results = await JsonSerializer.DeserializeAsync<List<NominatimResult>>(stream, cancellationToken: cancellationToken);
            var suggestions = results?
                .Select(ToAddressSuggestion)
                .Where(suggestion => suggestion is not null)
                .Select(suggestion => suggestion!)
                .Take(normalizedLimit)
                .ToList() ?? [];

            _suggestionCache[cacheKey] = suggestions;
            return suggestions;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Nominatim address suggestion search failed.");
            return [];
        }
    }

    public async Task<GeocodingResult?> FindCoordinatesAsync(string address, string city, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        return await FindCoordinatesForQueryAsync($"{address.Trim()}, {city.Trim()}, Romania", cancellationToken);
    }

    private async Task<GeocodingResult?> FindCoordinatesForQueryAsync(string query, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var requestUri = $"search?format=json&limit=1&countrycodes=ro&q={encodedQuery}";

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nominatim geocoding failed with status {StatusCode}.", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var results = await JsonSerializer.DeserializeAsync<List<NominatimResult>>(stream, cancellationToken: cancellationToken);
            var result = results?.FirstOrDefault();

            if (result is null ||
                !double.TryParse(result.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(result.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                return null;
            }

            return latitude is < -90 or > 90 || longitude is < -180 or > 180
                ? null
                : new GeocodingResult(latitude, longitude, result.DisplayName ?? string.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Nominatim geocoding request failed.");
            return null;
        }
    }

    public async Task<ReverseGeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return null;
        }

        var requestUri = string.Create(CultureInfo.InvariantCulture, $"reverse?format=jsonv2&lat={latitude}&lon={longitude}&zoom=18&addressdetails=1");

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nominatim reverse geocoding failed with status {StatusCode}.", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<NominatimReverseResult>(stream, cancellationToken: cancellationToken);

            if (result is null || string.IsNullOrWhiteSpace(result.DisplayName))
            {
                return null;
            }

            var city = FirstNonEmpty(
                result.Address?.City,
                result.Address?.Town,
                result.Address?.Village,
                result.Address?.Municipality,
                result.Address?.County);

            return new ReverseGeocodingResult(
                latitude,
                longitude,
                result.DisplayName,
                city,
                result.Address?.Road,
                result.Address?.HouseNumber);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Nominatim reverse geocoding request failed.");
            return null;
        }
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimReverseResult
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress
    {
        [JsonPropertyName("road")]
        public string? Road { get; set; }

        [JsonPropertyName("house_number")]
        public string? HouseNumber { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("town")]
        public string? Town { get; set; }

        [JsonPropertyName("village")]
        public string? Village { get; set; }

        [JsonPropertyName("municipality")]
        public string? Municipality { get; set; }

        [JsonPropertyName("county")]
        public string? County { get; set; }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static AddressSuggestion? ToAddressSuggestion(NominatimResult result)
    {
        if (string.IsNullOrWhiteSpace(result.DisplayName) ||
            !double.TryParse(result.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(result.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            latitude is < -90 or > 90 ||
            longitude is < -180 or > 180)
        {
            return null;
        }

        var city = FirstNonEmpty(
            result.Address?.City,
            result.Address?.Town,
            result.Address?.Village,
            result.Address?.Municipality,
            result.Address?.County);

        var addressParts = new[] { result.Address?.Road, result.Address?.HouseNumber }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var address = string.Join(" ", addressParts);

        return new AddressSuggestion(
            result.DisplayName,
            latitude,
            longitude,
            city,
            string.IsNullOrWhiteSpace(address) ? null : address);
    }
}
