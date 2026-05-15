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
    public async Task SearchAddressSuggestionsAsync_ReturnsSuggestionsFromNominatimResponse()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            [
              {
                "lat": "46.7712",
                "lon": "23.6236",
                "display_name": "Strada Dunarii 69, Cluj-Napoca, Romania",
                "address": {
                  "road": "Strada Dunarii",
                  "house_number": "69",
                  "city": "Cluj-Napoca"
                }
              }
            ]
            """))
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        var results = await service.SearchAddressSuggestionsAsync("Strada Dun", 5);

        var suggestion = Assert.Single(results);
        Assert.Equal("Strada Dunarii 69, Cluj-Napoca, Romania", suggestion.DisplayName);
        Assert.Equal(46.7712, suggestion.Latitude);
        Assert.Equal(23.6236, suggestion.Longitude);
        Assert.Equal("Cluj-Napoca", suggestion.City);
        Assert.Equal("Strada Dunarii 69", suggestion.Address);
    }

    [Fact]
    public async Task SearchAddressSuggestionsAsync_ShortQueryDoesNotCallNominatim()
    {
        var handler = new FakeHttpMessageHandler("[]");
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        var results = await service.SearchAddressSuggestionsAsync("Cl");

        Assert.Empty(results);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SearchAddressSuggestionsAsync_ReturnsEmptyWhenRequestFails()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler("[]", HttpStatusCode.TooManyRequests))
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        var results = await service.SearchAddressSuggestionsAsync("Strada Dunarii");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAddressSuggestionsAsync_UsesCachedResultForRepeatedQuery()
    {
        var handler = new FakeHttpMessageHandler("""
            [
              {
                "lat": "46.7712",
                "lon": "23.6236",
                "display_name": "Cluj-Napoca, Romania"
              }
            ]
            """);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var service = new NominatimGeocodingService(httpClient, NullLogger<NominatimGeocodingService>.Instance);

        await service.SearchAddressSuggestionsAsync("Cluj");
        await service.SearchAddressSuggestionsAsync("Cluj");

        Assert.Equal(1, handler.RequestCount);
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
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };

            return Task.FromResult(response);
        }
    }
}
