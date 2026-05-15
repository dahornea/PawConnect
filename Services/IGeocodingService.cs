namespace PawConnect.Services;

public interface IGeocodingService
{
    Task<GeocodingResult?> FindCoordinatesAsync(string query, CancellationToken cancellationToken = default);

    Task<GeocodingResult?> FindCoordinatesAsync(string address, string city, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AddressSuggestion>> SearchAddressSuggestionsAsync(string query, int limit = 5, CancellationToken cancellationToken = default);

    Task<ReverseGeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
