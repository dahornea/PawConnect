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
    }

    [Fact]
    public async Task PreviewShelterResourcesImportAsync_InvalidQuantityFailsValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new CsvImportService(context);

        var result = await service.PreviewShelterResourcesImportAsync(StreamFrom("""
            Name,Category,FoodType,Quantity,Unit,LowStockThreshold
            Adult dry food,Food,Adult dry food,-1,kg,10
            """), TestDbContextFactory.ShelterId);

        var row = Assert.Single(result.RowResults);
        Assert.False(row.IsValid);
        Assert.Contains(row.Errors, error => error.Message == "Quantity must be greater than zero.");
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
        Assert.Contains(row.Errors, error => error.Message.Contains("valid direct image URL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAdminShelterRequestsAsync_ValidCsvCreatesPendingRequestAndNotification()
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
        Assert.Single(notificationService.CreatedNotifications);
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

        public Task<List<Notification>> GetNotificationsForUserAsync(string userId, int count = 20) => Task.FromResult(new List<Notification>());

        public Task<List<Notification>> GetNotificationsForUserAsync(NotificationCategory? category, bool unreadOnly, int count = 100) => Task.FromResult(new List<Notification>());

        public Task<List<Notification>> GetNotificationsForUserAsync(string userId, NotificationCategory? category, bool unreadOnly, int count = 100) => Task.FromResult(new List<Notification>());

        public Task<int> GetUnreadCountAsync(string userId) => Task.FromResult(0);

        public Task MarkAsReadAsync(int notificationId, string userId) => Task.CompletedTask;

        public Task MarkAllAsReadAsync(string userId) => Task.CompletedTask;

        public Task DeleteNotificationAsync(int notificationId, string userId) => Task.CompletedTask;
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
