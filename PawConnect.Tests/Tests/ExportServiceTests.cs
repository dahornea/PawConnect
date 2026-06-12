using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class ExportServiceTests
{
    [Fact]
    public async Task GenerateUsersCsvAsync_ContainsUserData_AndExcludesSensitiveIdentityFields()
    {
        using var context = TestDbContextFactory.CreateContext();
        var user = context.Users.First(user => user.Id == TestDbContextFactory.AdopterId);
        user.PasswordHash = "secret-password-hash";
        user.SecurityStamp = "secret-security-stamp";
        user.ConcurrencyStamp = "secret-concurrency-stamp";
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var csv = Decode((await service.GenerateUsersCsvAsync()).Content);

        Assert.Contains("adopter@test.com", csv);
        Assert.Contains(IdentitySeedData.AdopterRole, csv);
        Assert.DoesNotContain("secret-password-hash", csv);
        Assert.DoesNotContain("secret-security-stamp", csv);
        Assert.DoesNotContain("secret-concurrency-stamp", csv);
        Assert.DoesNotContain("PasswordHash", csv);
    }

    [Fact]
    public async Task GenerateShelterDogsCsvAsync_ContainsOnlyCurrentShelterDogs()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Shelter Puppy", shelterId: TestDbContextFactory.ShelterId),
            TestDbContextFactory.CreateDog("Other Shelter Dog", shelterId: TestDbContextFactory.OtherShelterId));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var csv = Decode((await service.GenerateShelterDogsCsvAsync(TestDbContextFactory.ShelterId)).Content);

        Assert.Contains("Shelter Puppy", csv);
        Assert.DoesNotContain("Other Shelter Dog", csv);
    }

    [Fact]
    public async Task GenerateAdoptionRequestsCsvAsync_ContainsRequestStatusDogAndAdopterData()
    {
        using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Bella");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.Add(new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            Status = AdoptionRequestStatus.VisitConfirmed,
            VisitStatus = AdoptionVisitStatus.Confirmed,
            PreferredVisitDateTime = DateTime.UtcNow.AddDays(2),
            ReasonForAdoption = "Ready to adopt.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var csv = Decode((await service.GenerateAdoptionRequestsCsvAsync()).Content);

        Assert.Contains("Bella", csv);
        Assert.Contains("adopter@test.com", csv);
        Assert.Contains("VisitConfirmed", csv);
    }

    [Fact]
    public async Task GenerateShelterResourcesCsvAsync_ContainsOnlyCurrentShelterResourcesAndLowStockStatus()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.AddRange(
            new ResourceStock
            {
                ShelterId = TestDbContextFactory.ShelterId,
                Name = "Adult food",
                ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
                FoodTypeId = TestDbContextFactory.AdultFoodTypeId,
                Quantity = 4,
                Unit = "kg",
                LowStockThreshold = 5
            },
            new ResourceStock
            {
                ShelterId = TestDbContextFactory.OtherShelterId,
                Name = "Other shelter medicine",
                ResourceCategoryId = TestDbContextFactory.MedicineCategoryId,
                Quantity = 10,
                Unit = "boxes",
                LowStockThreshold = 2
            });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var csv = Decode((await service.GenerateShelterResourcesCsvAsync(TestDbContextFactory.ShelterId)).Content);

        Assert.Contains("Adult food", csv);
        Assert.Contains("Yes", csv);
        Assert.DoesNotContain("Other shelter medicine", csv);
    }

    [Fact]
    public async Task GenerateAdoptionRequestsPdfAsync_ReturnsPdfBytes()
    {
        using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var file = await service.GenerateAdoptionRequestsPdfAsync();

        Assert.Equal("application/pdf", file.ContentType);
        Assert.StartsWith("%PDF", Encoding.UTF8.GetString(file.Content[..4]));
    }

    private static ExportService CreateService(ApplicationDbContext context)
    {
        return new ExportService(
            context,
            TestDbContextFactory.CreateUserManager(context),
            NullLogger<ExportService>.Instance);
    }

    private static string Decode(byte[] content)
    {
        return Encoding.UTF8.GetString(content);
    }
}
