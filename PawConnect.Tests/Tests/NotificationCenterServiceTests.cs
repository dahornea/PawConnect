using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class NotificationCenterServiceTests
{
    [Fact]
    public async Task GetNotificationsAsync_ReturnsOnlyCurrentUsersNotifications()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        context.Notifications.AddRange(
            Notification(TestDbContextFactory.AdopterId, "Own update"),
            Notification(TestDbContextFactory.SecondAdopterId, "Other update"));
        await context.SaveChangesAsync();
        var service = CreateService(databaseName);

        var result = await service.GetNotificationsAsync(
            TestDbContextFactory.AdopterId,
            new NotificationCenterQuery());

        var item = Assert.Single(result.Groups.SelectMany(group => group.Items));
        Assert.Equal("Own update", item.Title);
        Assert.Equal(1, result.UnreadCount);
    }

    [Fact]
    public async Task GetNotificationsAsync_FiltersByCategoryReadStateAndSearch()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        context.Notifications.AddRange(
            Notification(TestDbContextFactory.ShelterUserId, "Low food stock", NotificationCategory.Resource, isRead: false),
            Notification(TestDbContextFactory.ShelterUserId, "Adoption update", NotificationCategory.Adoption, isRead: false),
            Notification(TestDbContextFactory.ShelterUserId, "Read resource note", NotificationCategory.Resource, isRead: true));
        await context.SaveChangesAsync();
        var service = CreateService(databaseName);

        var result = await service.GetNotificationsAsync(
            TestDbContextFactory.ShelterUserId,
            new NotificationCenterQuery(NotificationCategory.Resource, NotificationReadState.Unread, "food"));

        var item = Assert.Single(result.Groups.SelectMany(group => group.Items));
        Assert.Equal("Low food stock", item.Title);
        Assert.Equal(NotificationCategory.Resource, item.Category);
        Assert.False(item.IsRead);
    }

    [Fact]
    public async Task MarkAsUnreadAsync_ClearsReadStateForOwnerOnly()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var notification = Notification(TestDbContextFactory.AdopterId, "Read notification", isRead: true);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var service = CreateService(databaseName);

        await service.MarkAsUnreadAsync(notification.Id, TestDbContextFactory.SecondAdopterId);

        var saved = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.True(saved.IsRead);

        await service.MarkAsUnreadAsync(notification.Id, TestDbContextFactory.AdopterId);

        saved = await context.Notifications.AsNoTracking().SingleAsync();
        Assert.False(saved.IsRead);
        Assert.Null(saved.ReadAt);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_AffectsOnlyCurrentUser()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        context.Notifications.AddRange(
            Notification(TestDbContextFactory.AdopterId, "Own unread"),
            Notification(TestDbContextFactory.SecondAdopterId, "Other unread"));
        await context.SaveChangesAsync();
        var service = CreateService(databaseName);

        await service.MarkAllAsReadAsync(TestDbContextFactory.AdopterId);

        var own = await context.Notifications.AsNoTracking().SingleAsync(item => item.UserId == TestDbContextFactory.AdopterId);
        var other = await context.Notifications.AsNoTracking().SingleAsync(item => item.UserId == TestDbContextFactory.SecondAdopterId);
        Assert.True(own.IsRead);
        Assert.False(other.IsRead);
    }

    [Fact]
    public async Task GetNotificationsAsync_HidesUnsafeExternalLinks()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        context.Notifications.AddRange(
            Notification(TestDbContextFactory.AdopterId, "Safe link", link: "/dogs/1"),
            Notification(TestDbContextFactory.AdopterId, "External link", link: "https://example.com"));
        await context.SaveChangesAsync();
        var service = CreateService(databaseName);

        var result = await service.GetNotificationsAsync(
            TestDbContextFactory.AdopterId,
            new NotificationCenterQuery());
        var items = result.Groups.SelectMany(group => group.Items).ToList();

        Assert.Equal("/dogs/1", items.Single(item => item.Title == "Safe link").RelatedUrl);
        Assert.Null(items.Single(item => item.Title == "External link").RelatedUrl);
    }

    private static NotificationCenterService CreateService(string databaseName)
    {
        return new NotificationCenterService(TestDbContextFactory.CreateContextFactory(databaseName));
    }

    private static Notification Notification(
        string userId,
        string title,
        NotificationCategory category = NotificationCategory.System,
        bool isRead = false,
        string? link = null)
    {
        return new Notification
        {
            UserId = userId,
            Title = title,
            Message = $"{title} message",
            Category = category,
            Type = NotificationType.Info,
            Link = link,
            IsRead = isRead,
            ReadAt = isRead ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };
    }
}
