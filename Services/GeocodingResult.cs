namespace PawConnect.Services;

public sealed record GeocodingResult(double Latitude, double Longitude, string DisplayName);

public sealed record ReverseGeocodingResult(
    double Latitude,
    double Longitude,
    string DisplayName,
    string? City,
    string? Road,
    string? HouseNumber)
{
    public string? SuggestedAddress
    {
        get
        {
            var parts = new[] { Road, HouseNumber }
                .Where(part => !string.IsNullOrWhiteSpace(part));

            var address = string.Join(" ", parts);
            return string.IsNullOrWhiteSpace(address) ? DisplayName : address;
        }
    }
}
