namespace PawConnect.Services;

public interface IGeocodingService
{
    Task<GeocodingResult?> FindCoordinatesAsync(string address, string city, CancellationToken cancellationToken = default);

    Task<ReverseGeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
