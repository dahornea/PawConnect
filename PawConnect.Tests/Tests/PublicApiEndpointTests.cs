using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PawConnect.Data;
using PawConnect.DTOs.Api;
using PawConnect.Entities;

namespace PawConnect.Tests.Tests;

public class PublicApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task SwaggerJson_LoadsPublicApiDocument()
    {
        await using var factory = new PawConnectApiFactory();
        var client = factory.CreateClient();

        var swagger = await client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("PawConnect Public API", swagger);
        Assert.Contains("/api/v1/dogs", swagger);
        Assert.Contains("/api/v1/shelters", swagger);
    }

    [Fact]
    public async Task PublicApiResponse_IncludesSecurityHeaders()
    {
        await using var factory = new PawConnectApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dogs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "SAMEORIGIN");
        AssertHeader(response, "Referrer-Policy", "strict-origin-when-cross-origin");
        AssertHeader(response, "Permissions-Policy", "camera=(), microphone=(), payment=(), usb=(), geolocation=(self)");
    }
    [Fact]
    public async Task DogsEndpoint_ReturnsOnlyPublicSafeDogs()
    {
        await using var factory = new PawConnectApiFactory();
        await factory.SeedAsync(context =>
        {
            context.Shelters.Add(CreateShelter());
            context.Dogs.AddRange(
                CreateDog("Available Api Dog", DogStatus.Available),
                CreateDog("Reserved Api Dog", DogStatus.Reserved),
                CreateDog("Adopted Api Dog", DogStatus.Adopted),
                CreateDog("Treatment Api Dog", DogStatus.InTreatment));
        });
        var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<ApiPagedResult<DogListItemApiDto>>(
            "/api/v1/dogs",
            JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, dog => dog.Name == "Available Api Dog");
        Assert.Contains(result.Items, dog => dog.Name == "Reserved Api Dog");
        Assert.DoesNotContain(result.Items, dog => dog.Name == "Adopted Api Dog");
        Assert.DoesNotContain(result.Items, dog => dog.Name == "Treatment Api Dog");
    }

    [Fact]
    public async Task DogDetailsEndpoint_ReturnsNotFoundForNonPublicDog()
    {
        await using var factory = new PawConnectApiFactory();
        await factory.SeedAsync(context =>
        {
            context.Shelters.Add(CreateShelter());
            context.Dogs.Add(CreateDog("Adopted Api Dog", DogStatus.Adopted, id: 10));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dogs/10");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdoptionApplicationsEndpoint_ReturnsUnauthorizedForAnonymousUser()
    {
        await using var factory = new PawConnectApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/v1/adoption-applications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static void AssertHeader(HttpResponseMessage response, string name, string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"Expected response header '{name}'.");
        Assert.Contains(expectedValue, values);
    }
    private static Shelter CreateShelter()
    {
        return new Shelter
        {
            Id = 900,
            Name = "API Test Shelter",
            Address = "Test Street 1",
            City = "Cluj-Napoca",
            Neighborhood = "Zorilor",
            Email = "api-shelter@pawconnect.local"
        };
    }

    private static Dog CreateDog(string name, DogStatus status, int id = 0)
    {
        return new Dog
        {
            Id = id,
            Name = name,
            Breed = "Labrador Retriever",
            Age = 3,
            AgeYears = 3,
            AgeMonths = 0,
            Size = DogSize.Medium,
            Location = "Cluj-Napoca",
            Status = status,
            ShelterId = 900,
            Description = $"{name} is used by the public API tests.",
            Images =
            [
                new DogImage
                {
                    ImageUrl = "https://example.com/dog.jpg",
                    IsMainImage = true
                }
            ]
        };
    }

    private sealed class PawConnectApiFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=PawConnectApiTests;Trusted_Connection=True;",
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["SeedData:Enabled"] = "false",
                    ["NotificationOutbox:Enabled"] = "false",
                    ["ScheduledReports:Enabled"] = "false",
                    ["VisitReminders:Enabled"] = "false",
                    ["EmailSettings:OpenLocalInboxOnStartup"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();

                foreach (var descriptor in services
                    .Where(descriptor => descriptor.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration", StringComparison.Ordinal) == true)
                    .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.AddDbContextFactory<ApplicationDbContext>(
                    options => options.UseInMemoryDatabase(databaseName),
                    ServiceLifetime.Scoped);
            });
        }

        public async Task SeedAsync(Action<ApplicationDbContext> seed)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            seed(context);
            await context.SaveChangesAsync();
        }
    }
}
