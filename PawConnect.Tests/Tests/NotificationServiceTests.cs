using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

        await test.Service.CreateNotificationAsync(
            TestDbContextFactory.AdopterId,
            "Adoption request accepted",
            "Your request for Bella was accepted.",
            NotificationCategory.Adoption,
            NotificationType.Success,
            "/my-adoption-requests",
            "AdoptionRequest",
            "12");

        var notification = await test.Context.Notifications.SingleAsync();
        Assert.Equal(TestDbContextFactory.AdopterId, notification.UserId);
        Assert.Equal(NotificationCategory.Adoption, notification.Category);
        Assert.Equal(NotificationType.Success, notification.Type);
        Assert.False(notification.IsRead);
        Assert.Equal(1, await test.Service.GetUnreadCountAsync(TestDbContextFactory.AdopterId));
    }

    [Fact]
    public async Task MarkAsReadAsync_OnlyMarksNotificationsOwnedByUser()
    {
        await using var test = CreateNotificationTestContext();
        await test.Service.CreateNotificationAsync(
            TestDbContextFactory.AdopterId,
            "Private notification",
            "This belongs to the first adopter.",
            NotificationCategory.System,
            NotificationType.Info);
        var notificationId = await test.Context.Notifications.Select(notification => notification.Id).SingleAsync();

        await test.Service.MarkAsReadAsync(notificationId, TestDbContextFactory.SecondAdopterId);

        var notification = await test.Context.Notifications.AsNoTracking().SingleAsync();
        Assert.False(notification.IsRead);

        await test.Service.MarkAsReadAsync(notificationId, TestDbContextFactory.AdopterId);

        test.Context.ChangeTracker.Clear();
        notification = await test.Context.Notifications.AsNoTracking().SingleAsync();
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
    }

    [Fact]
    public async Task GetNotificationsForUserAsync_ReturnsOwnNotificationsNewestFirst()
    {
        await using var test = CreateNotificationTestContext();
        test.Context.Notifications.AddRange(
            new Notification
            {
                UserId = TestDbContextFactory.AdopterId,
                Title = "Older",
                Message = "Older notification",
                Category = NotificationCategory.System,
                Type = NotificationType.Info,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new Notification
            {
                UserId = TestDbContextFactory.AdopterId,
                Title = "Newer",
                Message = "Newer notification",
                Category = NotificationCategory.Adoption,
                Type = NotificationType.Info,
                CreatedAt = DateTime.UtcNow
            },
            new Notification
            {
                UserId = TestDbContextFactory.SecondAdopterId,
                Title = "Other user",
                Message = "Should not be returned.",
                Category = NotificationCategory.System,
                Type = NotificationType.Info,
                CreatedAt = DateTime.UtcNow.AddMinutes(5)
            });
        await test.Context.SaveChangesAsync();

        var notifications = await test.Service.GetNotificationsForUserAsync(TestDbContextFactory.AdopterId);

        Assert.Equal(["Newer", "Older"], notifications.Select(notification => notification.Title).ToArray());
    }

    [Fact]
    public async Task GetNotificationsForUserAsync_FiltersByCategoryAndUnreadStatus()
    {
        await using var test = CreateNotificationTestContext();
        test.Context.Notifications.AddRange(
            new Notification
            {
                UserId = TestDbContextFactory.ShelterUserId,
                Title = "Low stock",
                Message = "Food is low.",
                Category = NotificationCategory.Resource,
                Type = NotificationType.Warning,
                IsRead = false
            },
            new Notification
            {
                UserId = TestDbContextFactory.ShelterUserId,
                Title = "Read adoption",
                Message = "Already read.",
                Category = NotificationCategory.Adoption,
                Type = NotificationType.Info,
                IsRead = true
            });
        await test.Context.SaveChangesAsync();

        var notifications = await test.Service.GetNotificationsForUserAsync(
            TestDbContextFactory.ShelterUserId,
            NotificationCategory.Resource,
            unreadOnly: true);

        var notification = Assert.Single(notifications);
        Assert.Equal("Low stock", notification.Title);
    }

    [Fact]
    public async Task CreateNotificationAsync_CanSuppressRecentDuplicate()
    {
        await using var test = CreateNotificationTestContext();

        await test.Service.CreateNotificationAsync(
            TestDbContextFactory.ShelterUserId,
            "Low stock",
            "Food is low.",
            NotificationCategory.Resource,
            NotificationType.Warning,
            suppressDuplicatesWithin: TimeSpan.FromMinutes(10));
        await test.Service.CreateNotificationAsync(
            TestDbContextFactory.ShelterUserId,
            "Low stock",
            "Food is low.",
            NotificationCategory.Resource,
            NotificationType.Warning,
            suppressDuplicatesWithin: TimeSpan.FromMinutes(10));

        Assert.Equal(1, await test.Context.Notifications.CountAsync());
    }

    private static NotificationTestContext CreateNotificationTestContext()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        var context = TestDbContextFactory.CreateContext(databaseName);
        var service = new NotificationService(
            TestDbContextFactory.CreateContextFactory(databaseName),
            NullLogger<NotificationService>.Instance);
        return new NotificationTestContext(context, service);
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
