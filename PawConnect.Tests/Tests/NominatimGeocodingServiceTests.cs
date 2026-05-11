using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Services;

namespace PawConnect.Tests.Tests;

public class NominatimGeocodingServiceTests
{
    [Fact]
    public async Task FindCoordinatesAsync_ReturnsCoordinatesFromNominatimResponse()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            [
              {
                "lat": "46.7509",
                "lon": "23.6022",
                "display_name": "Strada Buna Ziua 22, Cluj-Napoca, Romania"
              }
            ]
            """))
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        var result = await service.FindCoordinatesAsync("Strada Buna Ziua 22", "Cluj-Napoca");

        Assert.NotNull(result);
        Assert.Equal(46.7509, result!.Latitude);
        Assert.Equal(23.6022, result.Longitude);
    }

    [Fact]
    public async Task FindCoordinatesAsync_ReturnsNullWhenRequestFails()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler("[]", HttpStatusCode.TooManyRequests))
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        var result = await service.FindCoordinatesAsync("Missing Street", "Cluj-Napoca");

        Assert.Null(result);
    }

    [Fact]
    public async Task ReverseGeocodeAsync_ReturnsSuggestedAddressFromNominatimResponse()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            {
              "display_name": "Strada Buna Ziua 22, Cluj-Napoca, Romania",
              "address": {
                "road": "Strada Buna Ziua",
                "house_number": "22",
                "city": "Cluj-Napoca"
              }
            }
            """))
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        var result = await service.ReverseGeocodeAsync(46.7509, 23.6022);

        Assert.NotNull(result);
        Assert.Equal("Cluj-Napoca", result!.City);
        Assert.Equal("Strada Buna Ziua", result.Road);
        Assert.Equal("22", result.HouseNumber);
        Assert.Equal("Strada Buna Ziua 22", result.SuggestedAddress);
    }

    [Fact]
    public async Task ReverseGeocodeAsync_ReturnsNullForEmptyResponse()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler("{}"))
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        var result = await service.ReverseGeocodeAsync(46.7509, 23.6022);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReverseGeocodeAsync_ReturnsNullWhenRequestFails()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler("{}", HttpStatusCode.TooManyRequests))
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        var result = await service.ReverseGeocodeAsync(46.7509, 23.6022);

        Assert.Null(result);
    }

    private sealed class FakeHttpMessageHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };

            return Task.FromResult(response);
        }
    }
}
