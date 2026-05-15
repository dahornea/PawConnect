using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class DistanceServiceTests
{
    [Fact]
    public void CalculateDistanceKm_ReturnsZeroForSameCoordinate()
    {
        var service = new DistanceService();

        var distance = service.CalculateDistanceKm(46.7712, 23.6236, 46.7712, 23.6236);

        Assert.InRange(distance, 0, 0.001);
    }

    [Fact]
    public void CalculateDistanceKm_ReturnsExpectedDistanceBetweenClujNapocaAndBucharest()
    {
        var service = new DistanceService();

        var distance = service.CalculateDistanceKm(
            46.7712,
            23.6236,
            44.4268,
            26.1025);

        Assert.InRange(distance, 320, 330);
    }

    [Fact]
    public void CalculateDistanceKm_CanDistinguishFiveAndTwentyFiveKilometerRadii()
    {
        var service = new DistanceService();

        var distance = service.CalculateDistanceKm(
            46.7712,
            23.6236,
            46.7454,
            23.4937);

        Assert.True(distance > 5);
        Assert.True(distance < 25);
    }
}
