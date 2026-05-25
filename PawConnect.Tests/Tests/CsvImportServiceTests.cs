using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class CsvImportServiceTests
{
    [Fact]
    public async Task PreviewShelterResourcesImportAsync_ValidResourcesCsvParsesSuccessfully()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterResourcesImportAsync(StreamFrom("""
            Name,Category,FoodType,Quantity,Unit,LowStockThreshold
            Adult dry food,Food,Adult dry food,25,kg,10
            Blankets,Blankets,,12,pieces,5
            """), TestDbContextFactory.ShelterId);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ValidRows);
        Assert.False(result.HasErrors);
        Assert.All(result.RowResults, row => Assert.Equal(CsvImportActions.Create, row.Action));
    }

    [Fact]
    public async Task PreviewShelterResourcesImportAsync_InvalidNegativeQuantityFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterResourcesImportAsync(StreamFrom("""
            Name,Category,FoodType,Quantity,Unit,LowStockThreshold
            Adult dry food,Food,Adult dry food,-1,kg,10
            """), TestDbContextFactory.ShelterId);

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Quantity must be zero or greater.");
    }

    [Fact]
    public async Task PreviewShelterResourcesImportAsync_MissingNameFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterResourcesImportAsync(StreamFrom("""
            Name,Category,FoodType,Quantity,Unit,LowStockThreshold
            ,Food,Adult dry food,25,kg,10
            """), TestDbContextFactory.ShelterId);

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Name is required.");
    }

    [Fact]
    public async Task PreviewShelterResourcesImportAsync_DuplicateResourceRowIsDetected()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterResourcesImportAsync(StreamFrom("""
            Name,Category,FoodType,Quantity,Unit,LowStockThreshold
            Adult dry food,Food,Adult dry food,25,kg,10
            Adult dry food,Food,Adult dry food,30,kg,8
            """), TestDbContextFactory.ShelterId);

        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Contains(result.RowResults[1].Errors, error => error.Message == "Duplicate resource row in this CSV.");
    }

    [Fact]
    public async Task ImportShelterResourcesAsync_UpdatesExistingShelterResource()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.Add(new ResourceStock
        {
            ShelterId = TestDbContextFactory.ShelterId,
            Name = "Adult dry food",
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            FoodTypeId = TestDbContextFactory.AdultFoodTypeId,
            Quantity = 5,
            Unit = "kg",
            LowStockThreshold = 2
        });
        await context.SaveChangesAsync();
        var service = new CsvImportService(context);

        var result = await service.ImportShelterResourcesAsync(StreamFrom("""
            Name,Category,FoodType,Quantity,Unit,LowStockThreshold
            Adult dry food,Food,Adult dry food,40,kg,12
            """), TestDbContextFactory.ShelterId);

        var resource = Assert.Single(context.ResourceStocks.Where(resource => resource.ShelterId == TestDbContextFactory.ShelterId));
        Assert.Equal(1, result.ImportedRows);
        Assert.Equal(40, resource.Quantity);
        Assert.Equal(12, resource.LowStockThreshold);
    }

    [Fact]
    public async Task ImportShelterResourcesAsync_NonFoodResourceIgnoresFoodType()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        await service.ImportShelterResourcesAsync(StreamFrom("""
            Name,Category,FoodType,Quantity,Unit,LowStockThreshold
            Blankets,Blankets,Adult dry food,12,pieces,5
            """), TestDbContextFactory.ShelterId);

        var resource = Assert.Single(context.ResourceStocks.Where(resource => resource.Name == "Blankets"));
        Assert.Null(resource.FoodTypeId);
    }

    [Fact]
    public async Task ImportShelterResourcesAsync_AffectsOnlyCurrentShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.Add(new ResourceStock
        {
            ShelterId = TestDbContextFactory.OtherShelterId,
            Name = "Adult dry food",
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            FoodTypeId = TestDbContextFactory.AdultFoodTypeId,
            Quantity = 5,
            Unit = "kg",
            LowStockThreshold = 2
        });
        await context.SaveChangesAsync();
        var service = new CsvImportService(context);

        await service.ImportShelterResourcesAsync(StreamFrom("""
            Name,Category,FoodType,Quantity,Unit,LowStockThreshold
            Adult dry food,Food,Adult dry food,40,kg,12
            """), TestDbContextFactory.ShelterId);

        var otherShelterResource = Assert.Single(context.ResourceStocks.Where(resource => resource.ShelterId == TestDbContextFactory.OtherShelterId));
        var currentShelterResource = Assert.Single(context.ResourceStocks.Where(resource => resource.ShelterId == TestDbContextFactory.ShelterId));
        Assert.Equal(5, otherShelterResource.Quantity);
        Assert.Equal(40, currentShelterResource.Quantity);
    }

    [Fact]
    public async Task ImportShelterDogsAsync_ValidDogCsvCreatesDogAndImages()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.ImportShelterDogsAsync(StreamFrom("""
            Name,Breed,AgeYears,AgeMonths,Size,Status,Location,Description,PreferredFoodType,DailyFoodAmount,ImageUrls
            Buddy,Labrador Mix,2,6,Large,Available,Cluj-Napoca,Friendly dog,Adult dry food,480,"https://example.com/dog1.jpg;https://example.com/dog2.jpg"
            """), TestDbContextFactory.ShelterId);

        var dog = Assert.Single(context.Dogs.Where(dog => dog.Name == "Buddy"));
        Assert.Equal(1, result.ImportedRows);
        Assert.Equal(TestDbContextFactory.ShelterId, dog.ShelterId);
        Assert.Equal(DogStatus.Available, dog.Status);
        Assert.Equal("Labrador Retriever Mix", dog.Breed);
        Assert.True(dog.IsMixedBreed);
        Assert.Equal(DogBreedSeedData.Breeds.First(breed => breed.Name == "Labrador Retriever").Id, dog.DogBreedId);
        Assert.Equal(2, context.DogImages.Count(image => image.DogId == dog.Id));
        Assert.True(context.DogImages.Any(image => image.DogId == dog.Id && image.IsMainImage));
    }

    [Fact]
    public async Task ImportShelterDogsAsync_UnmatchedBreedStoresCustomBreed()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        await service.ImportShelterDogsAsync(StreamFrom("""
            Name,Breed,AgeYears,AgeMonths,Size,Status,Location,Description,PreferredFoodType,DailyFoodAmount,ImageUrls
            Nova,Rare Mountain Dog Mix,3,0,Medium,Available,Cluj-Napoca,Steady dog,Adult dry food,300,
            """), TestDbContextFactory.ShelterId);

        var dog = Assert.Single(context.Dogs.Where(dog => dog.Name == "Nova"));
        Assert.Null(dog.DogBreedId);
        Assert.True(dog.IsMixedBreed);
        Assert.Equal("Rare Mountain Dog", dog.CustomBreedName);
        Assert.Equal("Rare Mountain Dog Mix", dog.Breed);
    }

    [Fact]
    public async Task ImportShelterDogsAsync_KnownMixedBreedPairStoresPrimaryAndSecondaryBreeds()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        await service.ImportShelterDogsAsync(StreamFrom("""
            Name,Breed,AgeYears,AgeMonths,Size,Status,Location,Description,PreferredFoodType,DailyFoodAmount,ImageUrls
            Bailey,Labrador Retriever x Border Collie,2,0,Medium,Available,Cluj-Napoca,Smart and social dog,Adult dry food,360,
            """), TestDbContextFactory.ShelterId);

        var dog = Assert.Single(context.Dogs.Where(dog => dog.Name == "Bailey"));
        Assert.Equal(DogBreedSeedData.Breeds.First(breed => breed.Name == "Labrador Retriever").Id, dog.DogBreedId);
        Assert.Equal(DogBreedSeedData.Breeds.First(breed => breed.Name == "Border Collie").Id, dog.SecondaryBreedId);
        Assert.True(dog.IsMixedBreed);
        Assert.Equal("Labrador Retriever \u00d7 Border Collie Mix", dog.Breed);
    }

    [Fact]
    public async Task PreviewShelterDogsImportAsync_AgeMonthsTwelveFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterDogsImportAsync(StreamFrom("""
            Name,Breed,AgeYears,AgeMonths,Size,Status,Location,Description,PreferredFoodType,DailyFoodAmount,ImageUrls
            Buddy,Labrador Mix,2,12,Large,Available,Cluj-Napoca,Friendly dog,Adult dry food,480,
            """), TestDbContextFactory.ShelterId);

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Age in months must be between 0 and 11.");
    }

    [Fact]
    public async Task PreviewShelterDogsImportAsync_InvalidStatusFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterDogsImportAsync(StreamFrom("""
            Name,Breed,AgeYears,AgeMonths,Size,Status,Location,Description,PreferredFoodType,DailyFoodAmount,ImageUrls
            Buddy,Labrador Mix,2,6,Large,Unknown,Cluj-Napoca,Friendly dog,Adult dry food,480,
            """), TestDbContextFactory.ShelterId);

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Status must be Available, Reserved, Adopted, or InTreatment.");
    }

    [Fact]
    public async Task PreviewShelterDogsImportAsync_DuplicateImageUrlFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterDogsImportAsync(StreamFrom("""
            Name,Breed,AgeYears,AgeMonths,Size,Status,Location,Description,PreferredFoodType,DailyFoodAmount,ImageUrls
            Buddy,Labrador Mix,2,6,Large,Available,Cluj-Napoca,Friendly dog,Adult dry food,480,"https://example.com/dog.jpg;https://example.com/dog.jpg"
            """), TestDbContextFactory.ShelterId);

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Duplicate image URL in this row.");
    }

    [Fact]
    public async Task PreviewShelterDogsImportAsync_InvalidImageUrlFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterDogsImportAsync(StreamFrom("""
            Name,Breed,AgeYears,AgeMonths,Size,Status,Location,Description,PreferredFoodType,DailyFoodAmount,ImageUrls
            Buddy,Labrador Mix,2,6,Large,Available,Cluj-Napoca,Friendly dog,Adult dry food,480,"https://example.com/not-an-image"
            """), TestDbContextFactory.ShelterId);

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Image URL 'https://example.com/not-an-image' must be a valid direct image URL starting with http:// or https://.");
    }

    [Fact]
    public async Task ImportAdminShelterRequestsAsync_ValidCsvCreatesPendingRequestsOnly()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var notificationService = new FakeNotificationService();
        var service = new CsvImportService(context, notificationService: notificationService);
        var userCount = await context.Users.CountAsync();
        var shelterCount = await context.Shelters.CountAsync();

        var result = await service.ImportAdminShelterRequestsAsync(StreamFrom("""
            ShelterName,ContactPersonName,Email,PhoneNumber,City,Address,Description,Website,OpeningHours,ReasonForJoining,Latitude,Longitude
            Happy Tails Rescue,Alex Popescu,happytails@example.com,+40 700 000 100,Cluj-Napoca,Strada Exemplu 10,Fictional demo shelter,https://example.com,Mon-Fri 09:00-17:00,We want to list dogs,46.7712,23.6236
            """));

        var request = Assert.Single(context.ShelterRegistrationRequests.Where(request => request.Email == "happytails@example.com"));
        Assert.Equal(1, result.ImportedRows);
        Assert.Equal(ShelterRegistrationRequestStatus.Pending, request.Status);
        Assert.Equal(userCount, await context.Users.CountAsync());
        Assert.Equal(shelterCount, await context.Shelters.CountAsync());
        var notification = Assert.Single(notificationService.CreatedNotifications);
        Assert.Equal(TestDbContextFactory.AdminId, notification.UserId);
        Assert.Equal("Shelter requests imported", notification.Title);
        Assert.Contains("1 shelter application request(s)", notification.Message);
        Assert.Equal(NotificationCategory.ShelterApplication, notification.Category);
        Assert.Equal("/admin/shelter-requests", notification.Link);
    }

    [Fact]
    public async Task PreviewAdminShelterRequestsImportAsync_BlocksDuplicatePendingEmail()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.ShelterRegistrationRequests.Add(new ShelterRegistrationRequest
        {
            ShelterName = "Pending Shelter",
            ContactPersonName = "Pending Contact",
            Email = "pending@example.com",
            PhoneNumber = "+40 700 000 101",
            City = "Cluj-Napoca",
            Address = "Strada Pending 1",
            Description = "Existing pending request.",
            Status = ShelterRegistrationRequestStatus.Pending
        });
        await context.SaveChangesAsync();
        var service = new CsvImportService(context);

        var result = await service.PreviewAdminShelterRequestsImportAsync(StreamFrom("""
            ShelterName,ContactPersonName,Email,PhoneNumber,City,Address,Description,Website,OpeningHours,ReasonForJoining,Latitude,Longitude
            Pending Shelter 2,Alex Popescu, PENDING@example.com ,+40 700 000 100,Cluj-Napoca,Strada Exemplu 10,Fictional demo shelter,,,,,
            """));

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Equal(CsvImportActions.Duplicate, row.Action);
        Assert.Contains(row.Errors, error => error.Message == "A pending shelter application already exists for this email.");
    }

    [Fact]
    public async Task PreviewAdminShelterRequestsImportAsync_BlocksExistingShelterOrUserEmail()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewAdminShelterRequestsImportAsync(StreamFrom("""
            ShelterName,ContactPersonName,Email,PhoneNumber,City,Address,Description,Website,OpeningHours,ReasonForJoining,Latitude,Longitude
            Existing Shelter,Alex Popescu,shelter@test.com,+40 700 000 100,Cluj-Napoca,Strada Exemplu 10,Fictional demo shelter,,,,,
            """));

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Equal(CsvImportActions.Duplicate, row.Action);
        Assert.Contains(row.Errors, error => error.Message == "A shelter account with this email already exists.");
    }

    [Fact]
    public async Task PreviewAdminShelterRequestsImportAsync_InvalidEmailFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewAdminShelterRequestsImportAsync(StreamFrom("""
            ShelterName,ContactPersonName,Email,PhoneNumber,City,Address,Description,Website,OpeningHours,ReasonForJoining,Latitude,Longitude
            Broken Shelter,Alex Popescu,not-an-email,+40 700 000 100,Cluj-Napoca,Strada Exemplu 10,Fictional demo shelter,,,,,
            """));

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Email must be valid.");
    }

    [Fact]
    public async Task PreviewAdminShelterRequestsImportAsync_InvalidCoordinatesFailValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewAdminShelterRequestsImportAsync(StreamFrom("""
            ShelterName,ContactPersonName,Email,PhoneNumber,City,Address,Description,Website,OpeningHours,ReasonForJoining,Latitude,Longitude
            Broken Shelter,Alex Popescu,broken@example.com,+40 700 000 100,Cluj-Napoca,Strada Exemplu 10,Fictional demo shelter,,,,91,181
            """));

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Latitude must be between -90 and 90.");
        Assert.Contains(row.Errors, error => error.Message == "Longitude must be between -180 and 180.");
    }

    [Fact]
    public async Task ImportAdminShelterRequestsAsync_StoresAddressAndCitySeparately()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        await service.ImportAdminShelterRequestsAsync(StreamFrom("""
            ShelterName,ContactPersonName,Email,PhoneNumber,City,Address,Description,Website,OpeningHours,ReasonForJoining,Latitude,Longitude
            Happy Tails Rescue,Alex Popescu,happytails@example.com,+40 700 000 100,Cluj-Napoca,"Strada Exemplu 10, Cluj-Napoca",Fictional demo shelter,,,,,
            """));

        var request = Assert.Single(context.ShelterRegistrationRequests.Where(request => request.Email == "happytails@example.com"));
        Assert.Equal("Strada Exemplu 10", request.Address);
        Assert.Equal("Cluj-Napoca", request.City);
    }

    [Fact]
    public async Task ImportedShelterRequest_CanBeAcceptedThroughExistingApprovalFlow()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var importService = new CsvImportService(context);
        await importService.ImportAdminShelterRequestsAsync(StreamFrom("""
            ShelterName,ContactPersonName,Email,PhoneNumber,City,Address,Description,Website,OpeningHours,ReasonForJoining,Latitude,Longitude
            Happy Tails Rescue,Alex Popescu,happytails@example.com,+40 700 000 100,Cluj-Napoca,Strada Exemplu 10,Fictional demo shelter,https://example.com,Mon-Fri 09:00-17:00,We want to list dogs,46.7712,23.6236
            """));
        var request = await context.ShelterRegistrationRequests.SingleAsync(request => request.Email == "happytails@example.com");
        var userManager = TestDbContextFactory.CreateUserManager(context);
        var approvalService = new ShelterRegistrationRequestService(
            context,
            userManager,
            new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<ShelterRegistrationRequestService>.Instance);

        await approvalService.AcceptRequestAsync(request.Id, TestDbContextFactory.AdminId);

        var user = await userManager.FindByEmailAsync("happytails@example.com");
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, IdentitySeedData.ShelterRole));
        var shelter = Assert.Single(context.Shelters.Where(shelter => shelter.Email == "happytails@example.com"));
        Assert.Equal(user!.Id, shelter.ApplicationUserId);
        Assert.Equal(46.7712, shelter.Latitude);
        Assert.Equal(23.6236, shelter.Longitude);
    }

    private static MemoryStream StreamFrom(string csv)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(csv.ReplaceLineEndings("\n")));
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<CreatedNotification> CreatedNotifications { get; } = [];

        public Task CreateNotificationAsync(
            string userId,
            string title,
            string message,
            NotificationCategory category,
            NotificationType type,
            string? link = null,
            string? relatedEntityName = null,
            string? relatedEntityId = null,
            TimeSpan? suppressDuplicatesWithin = null)
        {
            CreatedNotifications.Add(new CreatedNotification(userId, title, message, category, type, link, relatedEntityName, relatedEntityId));
            return Task.CompletedTask;
        }

        public Task<List<Notification>> GetNotificationsForUserAsync(string userId, int count = 20)
        {
            return Task.FromResult(new List<Notification>());
        }

        public Task<List<Notification>> GetNotificationsForUserAsync(NotificationCategory? category, bool unreadOnly, int count = 100)
        {
            return Task.FromResult(new List<Notification>());
        }

        public Task<List<Notification>> GetNotificationsForUserAsync(string userId, NotificationCategory? category, bool unreadOnly, int count = 100)
        {
            return Task.FromResult(new List<Notification>());
        }

        public Task<int> GetUnreadCountAsync(string userId)
        {
            return Task.FromResult(0);
        }

        public Task MarkAsReadAsync(int notificationId, string userId)
        {
            return Task.CompletedTask;
        }

        public Task MarkAllAsReadAsync(string userId)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNotificationAsync(int notificationId, string userId)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record CreatedNotification(
        string UserId,
        string Title,
        string Message,
        NotificationCategory Category,
        NotificationType Type,
        string? Link,
        string? RelatedEntityName,
        string? RelatedEntityId);
}
