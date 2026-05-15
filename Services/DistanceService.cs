namespace PawConnect.Services;

public class DistanceService : IDistanceService
{
    private const double EarthRadiusKm = 6371.0088;

    public double CalculateDistanceKm(double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitude)
    {
        var originLatitudeRadians = ToRadians(originLatitude);
        var destinationLatitudeRadians = ToRadians(destinationLatitude);
        var latitudeDelta = ToRadians(destinationLatitude - originLatitude);
        var longitudeDelta = ToRadians(destinationLongitude - originLongitude);

        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
                        Math.Cos(originLatitudeRadians) *
                        Math.Cos(destinationLatitudeRadians) *
                        Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
