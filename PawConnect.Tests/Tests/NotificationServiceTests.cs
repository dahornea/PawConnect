using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateNotificationAsync_CreatesUnreadNotificationForUser()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var service = test.Service;

        await service.CreateNotificationAsync(
            TestDbContextFactory.AdopterId,
            "Adoption request accepted",
            "Your request for Bella was accepted.",
            NotificationCategory.Adoption,
            NotificationType.Success,
            "/my-adoption-requests",
            "AdoptionRequest",
            "12");

        var notification = await context.Notifications.SingleAsync();
        Assert.Equal(TestDbContextFactory.AdopterId, notification.UserId);
        Assert.Equal("Adoption request accepted", notification.Title);
        Assert.Equal(NotificationCategory.Adoption, notification.Category);
        Assert.Equal(NotificationType.Success, notification.Type);
        Assert.False(notification.IsRead);
        Assert.Equal(1, await service.GetUnreadCountAsync(TestDbContextFactory.AdopterId));
    }

    [Fact]
    public async Task MarkAsReadAsync_OnlyMarksNotificationsOwnedByUser()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var service = test.Service;
        await service.CreateNotificationAsync(
            TestDbContextFactory.AdopterId,
            "Private notification",
            "This belongs to the first adopter.",
            NotificationCategory.System,
            NotificationType.Info);
        var notificationId = await context.Notifications.Select(n => n.Id).SingleAsync();

        await service.MarkAsReadAsync(notificationId, TestDbContextFactory.SecondAdopterId);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.False(notification.IsRead);

        await service.MarkAsReadAsync(notificationId, TestDbContextFactory.AdopterId);

        context.ChangeTracker.Clear();
        notification = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
    }

    [Fact]
    public async Task GetNotificationsForUserAsync_ReturnsOwnNotificationsNewestFirst()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var older = new Notification
        {
            UserId = TestDbContextFactory.AdopterId,
            Title = "Older",
            Message = "Older notification",
            Category = NotificationCategory.System,
            Type = NotificationType.Info,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var newer = new Notification
        {
            UserId = TestDbContextFactory.AdopterId,
            Title = "Newer",
            Message = "Newer notification",
            Category = NotificationCategory.Adoption,
            Type = NotificationType.Info,
            CreatedAt = DateTime.UtcNow
        };
        var otherUserNotification = new Notification
        {
            UserId = TestDbContextFactory.SecondAdopterId,
            Title = "Other user",
            Message = "Should not be returned.",
            Category = NotificationCategory.System,
            Type = NotificationType.Info,
            CreatedAt = DateTime.UtcNow.AddMinutes(5)
        };
        context.Notifications.AddRange(older, newer, otherUserNotification);
        await context.SaveChangesAsync();
        var service = test.Service;

        var notifications = await service.GetNotificationsForUserAsync(TestDbContextFactory.AdopterId);

        Assert.Equal(["Newer", "Older"], notifications.Select(n => n.Title).ToArray());
    }

    [Fact]
    public async Task GetNotificationsForUserAsync_FiltersByCategoryAndUnreadStatus()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        context.Notifications.AddRange(
            new Notification
            {
                UserId = TestDbContextFactory.ShelterUserId,
                Title = "Low stock",
                Message = "Food is low.",
                Category = NotificationCategory.Resource,
                Type = NotificationType.Warning,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            },
            new Notification
            {
                UserId = TestDbContextFactory.ShelterUserId,
                Title = "Read resource",
                Message = "Already read.",
                Category = NotificationCategory.Resource,
                Type = NotificationType.Info,
                IsRead = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new Notification
            {
                UserId = TestDbContextFactory.ShelterUserId,
                Title = "Adoption",
                Message = "New request.",
                Category = NotificationCategory.Adoption,
                Type = NotificationType.Info,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            });
        await context.SaveChangesAsync();
        var service = test.Service;

        var resources = await service.GetNotificationsForUserAsync(
            TestDbContextFactory.ShelterUserId,
            NotificationCategory.Resource,
            unreadOnly: true);

        var notification = Assert.Single(resources);
        Assert.Equal("Low stock", notification.Title);
    }

    [Fact]
    public async Task CreateRequestAsync_CreatesShelterNotification()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var dog = TestDbContextFactory.CreateDog("Notification Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateAdoptionService(context, test.Service);

        await service.CreateRequestAsync(
            TestDbContextFactory.AdopterId,
            dog.Id,
            new AdoptionRequestQuestionnaire("I can offer a stable home.", 3, null));

        var notification = await context.Notifications.SingleAsync(n => n.UserId == TestDbContextFactory.ShelterUserId);
        Assert.Equal("New adoption request", notification.Title);
        Assert.Equal(NotificationCategory.Adoption, notification.Category);
        Assert.Equal(NotificationType.Info, notification.Type);
        Assert.Equal("/shelter/adoption-requests", notification.Link);
    }

    [Fact]
    public async Task AcceptRequestAsync_CreatesAdopterNotification()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var request = await SeedPendingRequestAsync(context, "Accepted Notification Dog");
        var service = CreateAdoptionService(context, test.Service);

        await service.AcceptRequestAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        var notification = await context.Notifications.SingleAsync(n => n.UserId == TestDbContextFactory.AdopterId);
        Assert.Equal("Adoption request accepted", notification.Title);
        Assert.Equal(NotificationType.Success, notification.Type);
        Assert.Contains("Accepted Notification Dog", notification.Message);
    }

    [Fact]
    public async Task RejectRequestAsync_CreatesAdopterNotification()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var request = await SeedPendingRequestAsync(context, "Rejected Notification Dog");
        var service = CreateAdoptionService(context, test.Service);

        await service.RejectRequestAsync(request.Id, TestDbContextFactory.ShelterId);

        var notification = await context.Notifications.SingleAsync(n => n.UserId == TestDbContextFactory.AdopterId);
        Assert.Equal("Adoption request rejected", notification.Title);
        Assert.Equal(NotificationType.Warning, notification.Type);
        Assert.Contains("Rejected Notification Dog", notification.Message);
    }

    [Fact]
    public async Task CreateLowStockResourceAsync_CreatesShelterNotification()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var service = CreateResourceService(context, test.Service);

        await service.CreateResourceAsync(new ResourceStock
        {
            Name = "Puppy food",
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            FoodTypeId = TestDbContextFactory.AdultFoodTypeId,
            Quantity = 2,
            Unit = "kg",
            LowStockThreshold = 5
        }, TestDbContextFactory.ShelterId);

        var notification = await context.Notifications.SingleAsync(n => n.UserId == TestDbContextFactory.ShelterUserId);
        Assert.Equal("Low stock resource", notification.Title);
        Assert.Equal(NotificationCategory.Resource, notification.Category);
        Assert.Equal(NotificationType.Warning, notification.Type);
        Assert.Contains("Puppy food", notification.Message);
    }

    [Fact]
    public async Task SubmitShelterRegistrationRequestAsync_CreatesAdminNotification()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var service = CreateShelterRegistrationRequestService(context, test.Service);

        await service.SubmitRequestAsync(CreateShelterRegistrationRequest());

        var notification = await context.Notifications.SingleAsync(n => n.UserId == TestDbContextFactory.AdminId);
        Assert.Equal("New shelter application", notification.Title);
        Assert.Equal(NotificationCategory.ShelterApplication, notification.Category);
        Assert.Equal(NotificationType.Info, notification.Type);
        Assert.Equal("/admin/shelter-requests", notification.Link);
    }

    [Fact]
    public async Task SendShelterSummaryReportAsync_CreatesShelterReportNotification()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var service = CreateShelterSummaryReportService(context, test.Service);

        await service.SendShelterSummaryReportAsync(TestDbContextFactory.ShelterId);

        var notification = await context.Notifications.SingleAsync(n => n.UserId == TestDbContextFactory.ShelterUserId);
        Assert.Equal("Summary report sent", notification.Title);
        Assert.Equal(NotificationCategory.Report, notification.Category);
        Assert.Equal(NotificationType.Success, notification.Type);
        Assert.Equal("/shelter/dashboard", notification.Link);
    }

    [Fact]
    public async Task CreateNotificationAsync_SuppressesRecentDuplicateWhenRequested()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var service = test.Service;

        await service.CreateNotificationAsync(
            TestDbContextFactory.ShelterUserId,
            "Summary report sent",
            "Your shelter summary report was sent by email.",
            NotificationCategory.Report,
            NotificationType.Success,
            "/shelter/dashboard",
            suppressDuplicatesWithin: TimeSpan.FromMinutes(60));

        await service.CreateNotificationAsync(
            TestDbContextFactory.ShelterUserId,
            "Summary report sent",
            "Your shelter summary report was sent by email.",
            NotificationCategory.Report,
            NotificationType.Success,
            "/shelter/dashboard",
            suppressDuplicatesWithin: TimeSpan.FromMinutes(60));

        Assert.Equal(1, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task CreateNotificationAsync_AllowsDuplicateWhenSuppressionIsNotRequested()
    {
        await using var test = CreateNotificationTestContext();
        var context = test.Context;
        var service = test.Service;

        await service.CreateNotificationAsync(
            TestDbContextFactory.ShelterUserId,
            "Summary report sent",
            "Your shelter summary report was sent by email.",
            NotificationCategory.Report,
            NotificationType.Success,
            "/shelter/dashboard");

        await service.CreateNotificationAsync(
            TestDbContextFactory.ShelterUserId,
            "Summary report sent",
            "Your shelter summary report was sent by email.",
            NotificationCategory.Report,
            NotificationType.Success,
            "/shelter/dashboard");

        Assert.Equal(2, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task GetUnreadCountAsync_CanRunConcurrently()
    {
        await using var test = CreateNotificationTestContext();
        var service = test.Service;
        await service.CreateNotificationAsync(
            TestDbContextFactory.AdopterId,
            "Concurrent notification",
            "This verifies independent notification contexts.",
            NotificationCategory.System,
            NotificationType.Info);

        var counts = await Task.WhenAll(
            service.GetUnreadCountAsync(TestDbContextFactory.AdopterId),
            service.GetUnreadCountAsync(TestDbContextFactory.AdopterId));

        Assert.Equal([1, 1], counts);
    }

    private static NotificationTestContext CreateNotificationTestContext()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        var context = TestDbContextFactory.CreateContext(databaseName);
        var service = CreateNotificationService(databaseName);
        return new NotificationTestContext(context, service);
    }

    private static NotificationService CreateNotificationService(string databaseName)
    {
        return new NotificationService(
            TestDbContextFactory.CreateContextFactory(databaseName),
            NullLogger<NotificationService>.Instance);
    }

    private static AdoptionRequestService CreateAdoptionService(
        ApplicationDbContext context,
        INotificationService notificationService)
    {
        return new AdoptionRequestService(
            context,
            new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<AdoptionRequestService>.Instance,
            TestDbContextFactory.CreateUserManager(context),
            notificationService);
    }

    private static ResourceStockService CreateResourceService(
        ApplicationDbContext context,
        INotificationService notificationService)
    {
        return new ResourceStockService(
            context,
            new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<ResourceStockService>.Instance,
            notificationService: notificationService);
    }

    private static ShelterRegistrationRequestService CreateShelterRegistrationRequestService(
        ApplicationDbContext context,
        INotificationService notificationService)
    {
        return new ShelterRegistrationRequestService(
            context,
            TestDbContextFactory.CreateUserManager(context),
            new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<ShelterRegistrationRequestService>.Instance,
            notificationService: notificationService);
    }

    private static ShelterSummaryReportService CreateShelterSummaryReportService(
        ApplicationDbContext context,
        INotificationService notificationService)
    {
        return new ShelterSummaryReportService(
            context,
            new TestEmailService(),
            new TestPdfReportService(),
            Options.Create(new ScheduledReportSettings { Enabled = true, ShelterReportIntervalMinutes = 5 }),
            NullLogger<ShelterSummaryReportService>.Instance,
            notificationService: notificationService);
    }

    private static async Task<AdoptionRequest> SeedPendingRequestAsync(ApplicationDbContext context, string dogName)
    {
        var dog = TestDbContextFactory.CreateDog(dogName);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            Status = AdoptionRequestStatus.Pending,
            ReasonForAdoption = "I am ready to adopt.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AdoptionRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    private static ShelterRegistrationRequest CreateShelterRegistrationRequest()
    {
        return new ShelterRegistrationRequest
        {
            ShelterName = "Notification Shelter",
            ContactPersonName = "Shelter Contact",
            Email = "notification-shelter@example.test",
            PhoneNumber = "+40 700 000 222",
            City = "Cluj-Napoca",
            Address = "Strada Test 22",
            Description = "A shelter application used for notification tests."
        };
    }

    private sealed class NotificationTestContext(ApplicationDbContext context, NotificationService service) : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; } = context;

        public NotificationService Service { get; } = service;

        public ValueTask DisposeAsync()
        {
            return Context.DisposeAsync();
        }
    }
}
