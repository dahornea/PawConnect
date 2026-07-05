using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class NotificationPreferenceDeliveryTests
{
    [Fact]
    public async Task PreferencesUseDefaultsAndSavingUpsertsRows()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = new NotificationPreferenceService(TestDbContextFactory.CreateContextFactory(databaseName));

        var defaults = await service.GetPreferencesAsync(TestDbContextFactory.AdopterId);
        Assert.Contains(defaults, preference =>
            preference.NotificationType == NotificationEventType.AdoptionRequestUpdates &&
            preference.InAppEnabled &&
            preference.EmailEnabled);

        await service.SavePreferencesAsync(
            TestDbContextFactory.AdopterId,
            [
                new(NotificationEventType.AdoptionRequestUpdates, false, false),
                new(NotificationEventType.AdoptionRequestUpdates, true, false)
            ]);

        var updated = await service.GetPreferencesAsync(TestDbContextFactory.AdopterId);
        var adoptionPreference = updated.Single(preference => preference.NotificationType == NotificationEventType.AdoptionRequestUpdates);

        Assert.True(adoptionPreference.InAppEnabled);
        Assert.False(adoptionPreference.EmailEnabled);
        Assert.Equal(1, await context.NotificationPreferences.CountAsync(preference =>
            preference.UserId == TestDbContextFactory.AdopterId &&
            preference.NotificationType == NotificationEventType.AdoptionRequestUpdates));
    }

    [Fact]
    public async Task NotificationServiceSkipsInAppNotificationWhenPreferenceDisabled()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var contextFactory = TestDbContextFactory.CreateContextFactory(databaseName);
        var preferenceService = new NotificationPreferenceService(contextFactory);
        var deliveryLogService = new NotificationDeliveryLogService(contextFactory);
        var notificationService = new NotificationService(
            contextFactory,
            NullLogger<NotificationService>.Instance,
            preferenceService,
            deliveryLogService);

        await preferenceService.SavePreferencesAsync(
            TestDbContextFactory.AdopterId,
            [new(NotificationEventType.AdoptionRequestUpdates, false, true)]);

        await notificationService.CreateNotificationAsync(
            TestDbContextFactory.AdopterId,
            "Adoption request update",
            "Your request changed status.",
            NotificationCategory.Adoption,
            NotificationType.Info);

        Assert.Empty(await context.Notifications.ToListAsync());
        var log = await context.NotificationDeliveryLogs.SingleAsync();
        Assert.Equal(NotificationDeliveryStatus.DisabledByPreference, log.Status);
        Assert.Equal(NotificationChannel.InApp, log.Channel);
        Assert.Equal(NotificationEventType.AdoptionRequestUpdates, log.NotificationType);
    }

    [Fact]
    public async Task NotificationServiceLogsSuccessfulInAppDeliveryWhenEnabled()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var contextFactory = TestDbContextFactory.CreateContextFactory(databaseName);
        var notificationService = new NotificationService(
            contextFactory,
            NullLogger<NotificationService>.Instance,
            new NotificationPreferenceService(contextFactory),
            new NotificationDeliveryLogService(contextFactory));

        await notificationService.CreateNotificationAsync(
            TestDbContextFactory.ShelterUserId,
            "Low stock resource",
            "Food is low.",
            NotificationCategory.Resource,
            NotificationType.Warning,
            "/shelter/resources",
            "ResourceStock",
            "15");

        var notification = await context.Notifications.SingleAsync();
        var log = await context.NotificationDeliveryLogs.SingleAsync();

        Assert.Equal(notification.Id, log.NotificationId);
        Assert.Equal(NotificationDeliveryStatus.Sent, log.Status);
        Assert.Equal(NotificationEventType.ResourceAlerts, log.NotificationType);
        Assert.Equal("ResourceStock", log.RelatedEntityType);
    }

    [Fact]
    public async Task DeliveryLogServiceAllowsAdminsToFilterLogsOnly()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        context.NotificationDeliveryLogs.AddRange(
            new NotificationDeliveryLog
            {
                UserId = TestDbContextFactory.AdopterId,
                NotificationType = NotificationEventType.AdoptionRequestUpdates,
                Channel = NotificationChannel.Email,
                Status = NotificationDeliveryStatus.Failed,
                Recipient = "adopter@test.com",
                Subject = "Adoption update",
                ErrorMessage = "SMTP timeout"
            },
            new NotificationDeliveryLog
            {
                UserId = TestDbContextFactory.ShelterUserId,
                NotificationType = NotificationEventType.ResourceAlerts,
                Channel = NotificationChannel.InApp,
                Status = NotificationDeliveryStatus.Sent,
                Subject = "Low stock"
            });
        await context.SaveChangesAsync();

        var service = new NotificationDeliveryLogService(TestDbContextFactory.CreateContextFactory(databaseName));

        var failed = await service.GetAdminDeliveryLogsAsync(
            new NotificationDeliveryLogFilter(Status: NotificationDeliveryStatus.Failed),
            TestDbContextFactory.AdminId);

        var log = Assert.Single(failed);
        Assert.Equal(NotificationDeliveryStatus.Failed, log.Status);
        Assert.Equal("a***@test.com", log.Recipient);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAdminDeliveryLogsAsync(
            new NotificationDeliveryLogFilter(),
            TestDbContextFactory.AdopterId));
    }

    [Fact]
    public async Task SmtpEmailServiceSkipsWorkflowEmailWhenEmailPreferenceDisabled()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var contextFactory = TestDbContextFactory.CreateContextFactory(databaseName);
        var preferenceService = new NotificationPreferenceService(contextFactory);
        var deliveryLogService = new NotificationDeliveryLogService(contextFactory);

        await preferenceService.SavePreferencesAsync(
            TestDbContextFactory.AdopterId,
            [new(NotificationEventType.AdoptionRequestUpdates, true, false)]);

        var emailService = new SmtpEmailService(
            Options.Create(new EmailSettings { SmtpHost = "localhost", SenderEmail = "no-reply@pawconnect.local" }),
            NullLogger<SmtpEmailService>.Instance,
            contextFactory,
            preferenceService,
            deliveryLogService);

        await emailService.SendEmailAsync(
            "adopter@test.com",
            "Adoption request update",
            "Your adoption request changed.");

        var log = await context.NotificationDeliveryLogs.SingleAsync();
        Assert.Equal(NotificationChannel.Email, log.Channel);
        Assert.Equal(NotificationDeliveryStatus.DisabledByPreference, log.Status);
        Assert.Equal(NotificationEventType.AdoptionRequestUpdates, log.NotificationType);
    }
}
