using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class NotificationOutboxServiceTests
{
    [Fact]
    public async Task EnqueueAsyncUsesIdempotencyKey()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = new NotificationOutboxService(TestDbContextFactory.CreateContextFactory(databaseName));

        var request = CreateRequest(idempotencyKey: "adoption-request-1");

        var first = await service.EnqueueAsync(request);
        var second = await service.EnqueueAsync(request);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await context.NotificationOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ProcessDueAsyncCreatesInAppNotificationAndMarksSent()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var factory = TestDbContextFactory.CreateContextFactory(databaseName);
        var outboxService = new NotificationOutboxService(factory);
        var processor = CreateProcessor(factory);

        await outboxService.EnqueueAsync(CreateRequest(channel: NotificationChannel.InApp));

        var result = await processor.ProcessDueAsync(10);

        var message = await context.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(1, result.Sent);
        Assert.Equal(NotificationOutboxStatus.Sent, message.Status);
        Assert.Equal(1, await context.Notifications.CountAsync());
        Assert.Equal(NotificationDeliveryStatus.Sent, (await context.NotificationDeliveryLogs.SingleAsync()).Status);
    }

    [Fact]
    public async Task ProcessDueAsyncRetriesThenDeadLettersAfterMaxAttempts()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var factory = TestDbContextFactory.CreateContextFactory(databaseName);
        var outboxService = new NotificationOutboxService(factory);
        var failingEmail = new TestEmailService { ThrowOnSend = true };
        var processor = CreateProcessor(factory, failingEmail);

        await outboxService.EnqueueAsync(CreateRequest(
            channel: NotificationChannel.Email,
            recipientEmail: "adopter@test.com",
            maxAttempts: 1));

        var result = await processor.ProcessDueAsync(10);

        var message = await context.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(1, result.DeadLettered);
        Assert.Equal(NotificationOutboxStatus.DeadLetter, message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.LastError);
    }

    [Fact]
    public async Task ProcessDueAsyncCancelsEmailWhenPreferenceDisabled()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var factory = TestDbContextFactory.CreateContextFactory(databaseName);
        var preferenceService = new NotificationPreferenceService(factory);
        var outboxService = new NotificationOutboxService(factory);
        var processor = CreateProcessor(factory, preferenceService: preferenceService);

        await preferenceService.SavePreferencesAsync(
            TestDbContextFactory.AdopterId,
            [new(NotificationEventType.AdoptionRequestUpdates, true, false)]);
        await outboxService.EnqueueAsync(CreateRequest(
            channel: NotificationChannel.Email,
            recipientEmail: "adopter@test.com"));

        var result = await processor.ProcessDueAsync(10);

        var message = await context.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(1, result.Skipped);
        Assert.Equal(NotificationOutboxStatus.Cancelled, message.Status);
        Assert.Equal(NotificationDeliveryStatus.DisabledByPreference, (await context.NotificationDeliveryLogs.SingleAsync()).Status);
    }

    [Fact]
    public async Task AdminCanRetryAndCancelOutboxMessages()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = new NotificationOutboxService(TestDbContextFactory.CreateContextFactory(databaseName));
        var message = await service.EnqueueAsync(CreateRequest());

        context.NotificationOutboxMessages.Single().Status = NotificationOutboxStatus.Failed;
        await context.SaveChangesAsync();

        await service.RetryAsync(message.Id, TestDbContextFactory.AdminId);
        Assert.Equal(NotificationOutboxStatus.Pending, await context.NotificationOutboxMessages.Select(outbox => outbox.Status).SingleAsync());

        await service.CancelAsync(message.Id, TestDbContextFactory.AdminId);
        Assert.Equal(NotificationOutboxStatus.Cancelled, await context.NotificationOutboxMessages.Select(outbox => outbox.Status).SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(message.Id, TestDbContextFactory.AdopterId));
    }

    private static NotificationOutboxCreateRequest CreateRequest(
        NotificationChannel channel = NotificationChannel.Email,
        string? recipientEmail = "adopter@test.com",
        string? idempotencyKey = null,
        int maxAttempts = 5)
    {
        return new NotificationOutboxCreateRequest(
            NotificationEventType.AdoptionRequestUpdates,
            channel,
            "Adoption request update",
            "Your adoption request changed.",
            RecipientUserId: TestDbContextFactory.AdopterId,
            RecipientEmail: recipientEmail,
            RelatedEntityType: "AdoptionRequest",
            RelatedEntityId: "1",
            IdempotencyKey: idempotencyKey,
            MaxAttempts: maxAttempts);
    }

    private static NotificationOutboxProcessor CreateProcessor(
        IDbContextFactory<ApplicationDbContext> factory,
        TestEmailService? emailService = null,
        NotificationPreferenceService? preferenceService = null)
    {
        return new NotificationOutboxProcessor(
            factory,
            emailService ?? new TestEmailService(),
            NullLogger<NotificationOutboxProcessor>.Instance,
            preferenceService ?? new NotificationPreferenceService(factory),
            new NotificationDeliveryLogService(factory));
    }
}
