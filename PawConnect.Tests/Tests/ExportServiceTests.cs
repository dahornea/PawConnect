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
            Breed = "Poodle \u00d7 Bichon Mix",
            DogBreedId = DogBreedSeedData.Breeds.First(breed => breed.Name == "Poodle").Id,
            SecondaryBreedId = DogBreedSeedData.Breeds.First(breed => breed.Name == "Bichon").Id,
            IsMixedBreed = true,
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
        Assert.Contains("Poodle \u00d7 Bichon Mix", csv);
        Assert.Contains("2 years, 6 months old", csv);
        Assert.Contains("Test Shelter", csv);
        Assert.Contains("Available", csv);
        Assert.Contains("Adult dry food", csv);
    }

    [Fact]
    public async Task GenerateShelterDogsCsvAsync_ContainsOnlyCurrentShelterDogs()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            new Dog
            {
                Name = "Shelter Puppy",
                Breed = "Mixed Breed",
                AgeYears = 0,
                AgeMonths = 7,
                Age = 0,
                Size = DogSize.Small,
                Location = "Cluj-Napoca",
                Status = DogStatus.Available,
                ShelterId = TestDbContextFactory.ShelterId,
                MedicalStatus = "Healthy",
                PreferredFoodTypeId = TestDbContextFactory.AdultFoodTypeId,
                DailyFoodAmountGrams = 250
            },
            TestDbContextFactory.CreateDog("Other Shelter Dog", shelterId: TestDbContextFactory.OtherShelterId));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var file = await service.GenerateShelterDogsCsvAsync(TestDbContextFactory.ShelterId);
        var csv = Decode(file.Content);

        Assert.Contains("Dog Id,Name,Breed,Age,Size,Location,Status,Preferred Food Type,Daily Food Amount Grams,Medical Status,AdoptedAt,Has Success Story", csv);
        Assert.Contains("Shelter Puppy", csv);
        Assert.Contains("7 months old", csv);
        Assert.Contains("Healthy", csv);
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
            Status = AdoptionRequestStatus.Pending,
            ReasonForAdoption = "Family companion",
            HoursAlonePerDay = 3,
            AdditionalInformation = "Has a quiet home."
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var file = await service.GenerateAdoptionRequestsCsvAsync();
        var csv = Decode(file.Content);

        Assert.Contains("Request Id,Dog Name,Shelter Name,Adopter Email,Status,PreferredVisitDateTime,VisitStatus,CreatedAt,UpdatedAt,ReasonForAdoption,HoursAlonePerDay,AdditionalInformation", csv);
        Assert.Contains("Bella", csv);
        Assert.Contains("Test Shelter", csv);
        Assert.Contains("adopter@test.com", csv);
        Assert.Contains("Pending", csv);
        Assert.Contains("Family companion", csv);
    }

    [Fact]
    public async Task GenerateShelterAdoptionRequestsCsvAsync_ContainsOnlyCurrentShelterRequests()
    {
        using var context = TestDbContextFactory.CreateContext();
        var shelterDog = TestDbContextFactory.CreateDog("Shelter Dog");
        var otherDog = TestDbContextFactory.CreateDog("Other Dog", shelterId: TestDbContextFactory.OtherShelterId);
        context.Dogs.AddRange(shelterDog, otherDog);
        context.AdopterProfiles.Add(new AdopterProfile
        {
            ApplicationUserId = TestDbContextFactory.AdopterId,
            FullName = "Profile Adopter",
            City = "Cluj-Napoca"
        });
        await context.SaveChangesAsync();

        context.AdoptionRequests.AddRange(
            new AdoptionRequest
            {
                DogId = shelterDog.Id,
                AdopterId = TestDbContextFactory.AdopterId,
                Status = AdoptionRequestStatus.Pending,
                ReasonForAdoption = "Ready to adopt",
                HoursAlonePerDay = 2,
                AdditionalInformation = "Has experience.",
                ShelterInternalNotes = "Private shelter note"
            },
            new AdoptionRequest
            {
                DogId = otherDog.Id,
                AdopterId = TestDbContextFactory.SecondAdopterId,
                Status = AdoptionRequestStatus.Pending,
                ReasonForAdoption = "Other shelter request"
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var file = await service.GenerateShelterAdoptionRequestsCsvAsync(TestDbContextFactory.ShelterId);
        var csv = Decode(file.Content);

        Assert.Contains("Request Id,Dog Name,Adopter Email,Adopter Full Name,Status,PreferredVisitDateTime,VisitStatus,CreatedAt,UpdatedAt,ReasonForAdoption,HoursAlonePerDay,AdditionalInformation,ShelterInternalNotes", csv);
        Assert.Contains("Shelter Dog", csv);
        Assert.Contains("Profile Adopter", csv);
        Assert.Contains("Private shelter note", csv);
        Assert.DoesNotContain("Other Dog", csv);
        Assert.DoesNotContain("Other shelter request", csv);
    }

    [Fact]
    public async Task GenerateShelterResourcesCsvAsync_ContainsOnlyCurrentShelterResourcesAndLowStockStatus()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.AddRange(
            new ResourceStock
            {
                Name = "Adult food",
                ShelterId = TestDbContextFactory.ShelterId,
                ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
                FoodTypeId = TestDbContextFactory.AdultFoodTypeId,
                Quantity = 3,
                Unit = "kg",
                LowStockThreshold = 5,
                LastUpdatedAt = DateTime.UtcNow
            },
            new ResourceStock
            {
                Name = "Other shelter medicine",
                ShelterId = TestDbContextFactory.OtherShelterId,
                ResourceCategoryId = TestDbContextFactory.MedicineCategoryId,
                Quantity = 10,
                Unit = "items",
                LowStockThreshold = 2,
                LastUpdatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var file = await service.GenerateShelterResourcesCsvAsync(TestDbContextFactory.ShelterId);
        var csv = Decode(file.Content);

        Assert.Contains("Resource Id,Name,Category,Food Type,Quantity,Unit,LowStockThreshold,IsLowStock,LastUpdatedAt", csv);
        Assert.Contains("Adult food", csv);
        Assert.Contains("Food", csv);
        Assert.Contains("Adult dry food", csv);
        Assert.Contains("Yes", csv);
        Assert.DoesNotContain("Other shelter medicine", csv);
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

    [Fact]
    public async Task GenerateShelterAdoptionRequestsPdfAsync_ReturnsPdfBytes()
    {
        using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var file = await service.GenerateShelterAdoptionRequestsPdfAsync(TestDbContextFactory.ShelterId);

        Assert.Equal("application/pdf", file.ContentType);
        Assert.StartsWith("%PDF", Encoding.UTF8.GetString(file.Content[..4]));
    }

    [Fact]
    public async Task GenerateShelterResourcesPdfAsync_ReturnsPdfBytes()
    {
        using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var file = await service.GenerateShelterResourcesPdfAsync(TestDbContextFactory.ShelterId);

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
