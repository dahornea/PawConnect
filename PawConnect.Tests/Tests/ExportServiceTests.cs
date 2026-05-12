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
        var user = context.Users.First(u => u.Id == TestDbContextFactory.AdopterId);
        user.PasswordHash = "secret-password-hash";
        user.SecurityStamp = "secret-security-stamp";
        user.ConcurrencyStamp = "secret-concurrency-stamp";
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var file = await service.GenerateUsersCsvAsync();
        var csv = Decode(file.Content);

        Assert.Equal("text/csv;charset=utf-8", file.ContentType);
        Assert.Contains("User Id,Email,UserName,Full Name,PhoneNumber,Roles,EmailConfirmed", csv);
        Assert.Contains("adopter@test.com", csv);
        Assert.Contains(IdentitySeedData.AdopterRole, csv);
        Assert.DoesNotContain("secret-password-hash", csv);
        Assert.DoesNotContain("secret-security-stamp", csv);
        Assert.DoesNotContain("secret-concurrency-stamp", csv);
        Assert.DoesNotContain("PasswordHash", csv);
        Assert.DoesNotContain("SecurityStamp", csv);
        Assert.DoesNotContain("ConcurrencyStamp", csv);
    }

    [Fact]
    public async Task GenerateDogsCsvAsync_ContainsDogShelterStatusAndFormattedAge()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.Dogs.Add(new Dog
        {
            Name = "Milo",
            Breed = "Mixed Breed",
            AgeYears = 2,
            AgeMonths = 6,
            Age = 2,
            Size = DogSize.Medium,
            Location = "Cluj-Napoca",
            Status = DogStatus.Available,
            ShelterId = TestDbContextFactory.ShelterId,
            PreferredFoodTypeId = TestDbContextFactory.AdultFoodTypeId,
            DailyFoodAmountGrams = 350
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var file = await service.GenerateDogsCsvAsync();
        var csv = Decode(file.Content);

        Assert.Contains("Dog Id,Name,Breed,Age,Size,Location,Shelter Name,Status,Preferred Food Type,Daily Food Amount Grams,Success Story,AdoptedAt", csv);
        Assert.Contains("Milo", csv);
        Assert.Contains("2 years, 6 months old", csv);
        Assert.Contains("Test Shelter", csv);
        Assert.Contains("Available", csv);
        Assert.Contains("Adult dry food", csv);
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
            Status = AdoptionRequestStatus.Pending,
            ReasonForAdoption = "Family companion",
            HoursAlonePerDay = 3,
            AdditionalInformation = "Has a quiet home."
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var file = await service.GenerateAdoptionRequestsCsvAsync();
        var csv = Decode(file.Content);

        Assert.Contains("Request Id,Dog Name,Shelter Name,Adopter Email,Status,CreatedAt,UpdatedAt,ReasonForAdoption,HoursAlonePerDay,AdditionalInformation", csv);
        Assert.Contains("Bella", csv);
        Assert.Contains("Test Shelter", csv);
        Assert.Contains("adopter@test.com", csv);
        Assert.Contains("Pending", csv);
        Assert.Contains("Family companion", csv);
    }

    [Fact]
    public async Task GenerateShelterRequestsCsvAsync_ContainsShelterApplicationData()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.ShelterRegistrationRequests.Add(new ShelterRegistrationRequest
        {
            ShelterName = "Future Shelter",
            ContactPersonName = "Future Contact",
            Email = "future@shelter.test",
            PhoneNumber = "0712345678",
            City = "Cluj-Napoca",
            Address = "Strada Test 10",
            Description = "Application for testing.",
            Status = ShelterRegistrationRequestStatus.Pending
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var file = await service.GenerateShelterRequestsCsvAsync();
        var csv = Decode(file.Content);

        Assert.Contains("Request Id,Shelter Name,Contact Person,Email,Phone,City,Address,Status,CreatedAt,ReviewedAt,ReviewedBy", csv);
        Assert.Contains("Future Shelter", csv);
        Assert.Contains("Future Contact", csv);
        Assert.Contains("future@shelter.test", csv);
        Assert.Contains("Pending", csv);
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

    [Fact]
    public async Task GenerateShelterRequestsPdfAsync_ReturnsPdfBytes()
    {
        using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var file = await service.GenerateShelterRequestsPdfAsync();

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
