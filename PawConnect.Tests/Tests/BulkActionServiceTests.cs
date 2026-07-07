using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class BulkActionServiceTests
{
    [Fact]
    public async Task UpdateShelterDogStatusAsync_UpdatesOnlyOwnEligibleDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var ownDog = TestDbContextFactory.CreateDog("Own dog", DogStatus.Available, TestDbContextFactory.ShelterId);
        var otherShelterDog = TestDbContextFactory.CreateDog("Other dog", DogStatus.Available, TestDbContextFactory.OtherShelterId);
        var adoptedDog = TestDbContextFactory.CreateDog("Adopted dog", DogStatus.Adopted, TestDbContextFactory.ShelterId);
        context.Dogs.AddRange(ownDog, otherShelterDog, adoptedDog);
        await context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = new BulkDogActionService(context, audit);

        var result = await service.UpdateShelterDogStatusAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            [ownDog.Id, otherShelterDog.Id, adoptedDog.Id, 999],
            DogStatus.InTreatment);

        Assert.Equal(4, result.TotalRequested);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(3, result.Failed);
        Assert.Equal(DogStatus.InTreatment, (await context.Dogs.SingleAsync(dog => dog.Id == ownDog.Id)).Status);
        Assert.Equal(DogStatus.Available, (await context.Dogs.SingleAsync(dog => dog.Id == otherShelterDog.Id)).Status);
        Assert.Equal(DogStatus.Adopted, (await context.Dogs.SingleAsync(dog => dog.Id == adoptedDog.Id)).Status);
        Assert.True(await context.DogStatusHistories.AnyAsync(history =>
            history.DogId == ownDog.Id &&
            history.OldStatus == DogStatus.Available &&
            history.NewStatus == DogStatus.InTreatment));
        Assert.Contains(audit.Logs, log => log.Action == AuditActions.BulkDogStatusUpdated);
    }

    [Fact]
    public async Task UpdateShelterDogStatusAsync_RejectsUnsupportedBulkStatus()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var audit = new RecordingAuditLogService();
        var service = new BulkDogActionService(context, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateShelterDogStatusAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                [1],
                DogStatus.Adopted));
    }

    [Fact]
    public async Task RetryAsync_ReturnsPartialSuccessForInvalidOutboxItems()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var outboxService = new NotificationOutboxService(TestDbContextFactory.CreateContextFactory(databaseName));
        var failed = await outboxService.EnqueueAsync(CreateRequest("failed"));
        var sent = await outboxService.EnqueueAsync(CreateRequest("sent"));
        var messages = await context.NotificationOutboxMessages.ToListAsync();
        messages.Single(message => message.Id == failed.Id).Status = NotificationOutboxStatus.Failed;
        messages.Single(message => message.Id == sent.Id).Status = NotificationOutboxStatus.Sent;
        await context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = new BulkNotificationOutboxActionService(outboxService, audit);

        var result = await service.RetryAsync(TestDbContextFactory.AdminId, [failed.Id, sent.Id, 999]);

        Assert.Equal(3, result.TotalRequested);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(2, result.Failed);
        context.ChangeTracker.Clear();
        Assert.Equal(NotificationOutboxStatus.Pending, (await context.NotificationOutboxMessages.SingleAsync(message => message.Id == failed.Id)).Status);
        Assert.Equal(NotificationOutboxStatus.Sent, (await context.NotificationOutboxMessages.SingleAsync(message => message.Id == sent.Id)).Status);
        Assert.Contains(audit.Logs, log => log.Action == AuditActions.BulkNotificationOutboxUpdated);
    }

    [Fact]
    public async Task CancelAsync_NonAdminDoesNotModifyOutboxMessages()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var outboxService = new NotificationOutboxService(TestDbContextFactory.CreateContextFactory(databaseName));
        var pending = await outboxService.EnqueueAsync(CreateRequest("pending"));
        var service = new BulkNotificationOutboxActionService(outboxService, new RecordingAuditLogService());

        var result = await service.CancelAsync(TestDbContextFactory.AdopterId, [pending.Id]);

        Assert.Equal(1, result.Failed);
        Assert.Equal(NotificationOutboxStatus.Pending, (await context.NotificationOutboxMessages.SingleAsync()).Status);
    }

    private static NotificationOutboxCreateRequest CreateRequest(string idempotencyKey)
    {
        return new NotificationOutboxCreateRequest(
            NotificationEventType.AdoptionRequestUpdates,
            NotificationChannel.Email,
            "Adoption request update",
            "Your adoption request changed.",
            RecipientUserId: TestDbContextFactory.AdopterId,
            RecipientEmail: "adopter@test.com",
            IdempotencyKey: idempotencyKey);
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<AuditLog> Logs { get; } = [];

        public Task LogAsync(AuditLog log)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task LogAsync(
            string action,
            string entityName,
            string? entityId,
            string description,
            string? userId = null,
            string? userEmail = null,
            string? userRole = null,
            string? additionalData = null)
        {
            Logs.Add(new AuditLog
            {
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Description = description,
                UserId = userId,
                UserEmail = userEmail,
                UserRole = userRole,
                AdditionalData = additionalData
            });
            return Task.CompletedTask;
        }

        public Task LogSystemAsync(string action, string entityName, string? entityId, string description, string? additionalData = null)
        {
            return LogAsync(action, entityName, entityId, description, additionalData: additionalData);
        }

        public Task LogUserActionAsync(string action, string entityType, string? entityId, string summary, object? details = null, string severity = "Information", string eventType = "Business")
        {
            return LogAsync(action, entityType, entityId, summary);
        }

        public Task LogSystemEventAsync(string action, string entityType, string? entityId, string summary, object? details = null, string severity = "Information")
        {
            return LogAsync(action, entityType, entityId, summary);
        }

        public Task LogCopilotEventAsync(string action, string? entityId, string summary, object? details = null, string severity = "Information")
        {
            return LogAsync(action, "AdoptionCopilot", entityId, summary);
        }

        public Task<List<AuditLog>> GetRecentLogsAsync(int count)
        {
            return Task.FromResult(Logs.Take(count).ToList());
        }

        public Task<List<AuditLog>> GetLogsAsync(string? action = null, string? entityName = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null, string? severity = null, string? eventType = null, string? correlationId = null, int take = 200)
        {
            return Task.FromResult(Logs.Take(take).ToList());
        }

        public Task<List<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId)
        {
            return Task.FromResult(Logs.Where(log => log.EntityName == entityName && log.EntityId == entityId).ToList());
        }
    }
}
