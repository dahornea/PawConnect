namespace PawConnect.Services;

public interface IDistanceService
{
    double CalculateDistanceKm(double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitude);
}
