using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.IntegrationTests.Infrastructure;
using PawConnect.Services;

namespace PawConnect.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public class SqlServerPersistenceTests(SqlServerTestcontainerFixture fixture)
{
    [DockerFact]
    public async Task Migrations_ShouldApplySuccessfully_AgainstSqlServerContainer()
    {
        await using var context = await fixture.CreateMigratedContextAsync();

        Assert.True(await context.Database.CanConnectAsync());
        Assert.Equal(0, await context.Dogs.CountAsync());
    }

    [DockerFact]
    public async Task Dog_ShouldPersist_WithShelterRelationship()
    {
        await using var context = await fixture.CreateMigratedContextAsync();
        await IntegrationTestData.SeedIdentityAndShelterAsync(context);
        var shelterId = await context.Shelters.Select(shelter => shelter.Id).SingleAsync();

        context.Dogs.Add(IntegrationTestData.CreateDog(shelterId, "SQL Bruno"));
        await context.SaveChangesAsync();

        await using var verificationContext = await fixture.CreateMigratedContextAsync(context.Database.GetDbConnection().Database);
        var dog = await verificationContext.Dogs
            .Include(dog => dog.Shelter)
            .SingleAsync(dog => dog.Name == "SQL Bruno");

        Assert.Equal("Integration Paws Shelter", dog.Shelter!.Name);
        Assert.Equal(DogStatus.Available, dog.Status);
        Assert.Equal("Golden", dog.CoatColor);
    }

    [DockerFact]
    public async Task AdoptionApplication_ShouldPersist_StatusTransition()
    {
        await using var context = await fixture.CreateMigratedContextAsync();
        await IntegrationTestData.SeedIdentityAndShelterAsync(context);
        var dog = IntegrationTestData.CreateDog(await context.Shelters.Select(shelter => shelter.Id).SingleAsync(), "SQL Adoption Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = IntegrationTestData.AdopterId,
            ReasonForAdoption = "I can offer a calm and stable home.",
            PreferredVisitDateTime = DateTime.UtcNow.AddDays(3),
            VisitStatus = AdoptionVisitStatus.Requested,
            Status = AdoptionRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AdoptionRequests.Add(request);
        await context.SaveChangesAsync();

        request.Status = AdoptionRequestStatus.VisitConfirmed;
        request.VisitStatus = AdoptionVisitStatus.Confirmed;
        request.VisitConfirmedByUserId = IntegrationTestData.ShelterUserId;
        request.VisitConfirmedAt = DateTime.UtcNow;
        dog.Status = DogStatus.Reserved;
        await context.SaveChangesAsync();

        await using var verificationContext = await fixture.CreateMigratedContextAsync(context.Database.GetDbConnection().Database);
        var saved = await verificationContext.AdoptionRequests
            .Include(adoptionRequest => adoptionRequest.Dog)
            .SingleAsync(adoptionRequest => adoptionRequest.Id == request.Id);

        Assert.Equal(AdoptionRequestStatus.VisitConfirmed, saved.Status);
        Assert.Equal(AdoptionVisitStatus.Confirmed, saved.VisitStatus);
        Assert.Equal(DogStatus.Reserved, saved.Dog!.Status);
    }

    [DockerFact]
    public async Task DogTransfer_ShouldPersistCompletion_AndMoveDogToDestinationShelter()
    {
        var databaseName = SqlServerTestcontainerFixture.CreateDatabaseName();
        await using var context = await fixture.CreateMigratedContextAsync(databaseName);
        await IntegrationTestData.SeedIdentityAndShelterAsync(context);
        var sourceShelterId = await context.Shelters.Select(shelter => shelter.Id).SingleAsync();
        var destinationUserId = "integration-destination-shelter";
        var destinationShelter = new Shelter
        {
            Name = "Integration Partner Shelter",
            Description = "Destination shelter used by transfer integration tests.",
            Address = "Partner Street 2",
            City = "Cluj-Napoca",
            Neighborhood = "Marasti",
            Email = "integration.partner@pawconnect.local",
            PhoneNumber = "+40 700 000 998",
            ApplicationUserId = destinationUserId
        };
        context.Users.Add(new ApplicationUser
        {
            Id = destinationUserId,
            UserName = "integration.partner@pawconnect.local",
            NormalizedUserName = "INTEGRATION.PARTNER@PAWCONNECT.LOCAL",
            Email = "integration.partner@pawconnect.local",
            NormalizedEmail = "INTEGRATION.PARTNER@PAWCONNECT.LOCAL",
            EmailConfirmed = true,
            FullName = "Integration Partner Shelter"
        });
        context.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = destinationUserId,
            RoleId = IdentitySeedData.ShelterRole
        });
        context.Shelters.Add(destinationShelter);
        var dog = IntegrationTestData.CreateDog(sourceShelterId, "SQL Transfer Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = new DogTransferService(context);

        var transfer = await service.CreateTransferRequestAsync(
            sourceShelterId,
            IntegrationTestData.ShelterUserId,
            new DogTransferCreateRequest(
                dog.Id,
                destinationShelter.Id,
                DogTransferPriority.Normal,
                "Partner shelter has room for this dog."));
        await service.ApproveTransferAsync(
            transfer.Id,
            destinationShelter.Id,
            destinationUserId,
            new DogTransferDecisionRequest("Accepted for transfer."));
        await service.CompleteTransferAsync(
            transfer.Id,
            sourceShelterId,
            IntegrationTestData.ShelterUserId);

        await using var verificationContext = await fixture.CreateMigratedContextAsync(databaseName);
        var savedDog = await verificationContext.Dogs.SingleAsync(savedDog => savedDog.Id == dog.Id);
        var savedTransfer = await verificationContext.DogTransferRequests.SingleAsync(request => request.Id == transfer.Id);

        Assert.Equal(destinationShelter.Id, savedDog.ShelterId);
        Assert.Equal(DogTransferStatus.Completed, savedTransfer.Status);
        Assert.NotNull(savedTransfer.CompletedAtUtc);
    }
    [DockerFact]
    public async Task NotificationPreferences_ShouldPersist_DisabledChannel()
    {
        var databaseName = SqlServerTestcontainerFixture.CreateDatabaseName();
        await using var context = await fixture.CreateMigratedContextAsync(databaseName);
        await IntegrationTestData.SeedIdentityAndShelterAsync(context);
        var service = new NotificationPreferenceService(fixture.CreateContextFactory(databaseName));

        await service.SavePreferencesAsync(
            IntegrationTestData.AdopterId,
            [new(NotificationEventType.AdoptionRequestUpdates, true, false)]);

        var preferences = await service.GetPreferencesAsync(IntegrationTestData.AdopterId);
        var adoptionPreference = preferences.Single(preference => preference.NotificationType == NotificationEventType.AdoptionRequestUpdates);

        Assert.True(adoptionPreference.InAppEnabled);
        Assert.False(adoptionPreference.EmailEnabled);
    }

    [DockerFact]
    public async Task NotificationOutbox_ShouldMarkMessageAsSent_WhenProcessingSucceeds()
    {
        var databaseName = SqlServerTestcontainerFixture.CreateDatabaseName();
        await using var context = await fixture.CreateMigratedContextAsync(databaseName);
        await IntegrationTestData.SeedIdentityAndShelterAsync(context);
        var factory = fixture.CreateContextFactory(databaseName);
        var outboxService = new NotificationOutboxService(factory);
        var processor = CreateOutboxProcessor(factory, new IntegrationTestEmailService());

        var queued = await outboxService.EnqueueAsync(CreateOutboxRequest());
        Assert.Equal(NotificationOutboxStatus.Pending, queued.Status);

        var result = await processor.ProcessDueAsync(10);

        var saved = await context.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(1, result.Sent);
        Assert.Equal(NotificationOutboxStatus.Sent, saved.Status);
        Assert.NotNull(saved.SentAt);
    }

    [DockerFact]
    public async Task NotificationOutbox_ShouldScheduleRetry_WhenProcessingFails()
    {
        var databaseName = SqlServerTestcontainerFixture.CreateDatabaseName();
        await using var context = await fixture.CreateMigratedContextAsync(databaseName);
        await IntegrationTestData.SeedIdentityAndShelterAsync(context);
        var factory = fixture.CreateContextFactory(databaseName);
        var outboxService = new NotificationOutboxService(factory);
        var processor = CreateOutboxProcessor(factory, new IntegrationTestEmailService { ThrowOnSend = true });

        await outboxService.EnqueueAsync(CreateOutboxRequest(maxAttempts: 3));

        var result = await processor.ProcessDueAsync(10);

        var saved = await context.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(1, result.Failed);
        Assert.Equal(NotificationOutboxStatus.Failed, saved.Status);
        Assert.Equal(1, saved.AttemptCount);
        Assert.NotNull(saved.NextAttemptAt);
        Assert.NotNull(saved.LastError);
    }

    [DockerFact]
    public async Task AuditLog_ShouldPersist_Metadata()
    {
        await using var context = await fixture.CreateMigratedContextAsync();
        await IntegrationTestData.SeedIdentityAndShelterAsync(context);

        context.AuditLogs.Add(new AuditLog
        {
            UserId = IntegrationTestData.AdminId,
            Action = "IntegrationTest",
            EntityName = nameof(Dog),
            EntityId = "42",
            Description = "SQL Server integration audit entry.",
            CorrelationId = "integration-correlation",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var log = await context.AuditLogs.SingleAsync(log => log.Action == "IntegrationTest");
        Assert.Equal(nameof(Dog), log.EntityName);
        Assert.Equal("integration-correlation", log.CorrelationId);
    }

    private static NotificationOutboxCreateRequest CreateOutboxRequest(int maxAttempts = 5)
    {
        return new NotificationOutboxCreateRequest(
            NotificationEventType.AdoptionRequestUpdates,
            NotificationChannel.Email,
            "SQL integration notification",
            "This notification is queued by a SQL Server integration test.",
            RecipientUserId: IntegrationTestData.AdopterId,
            RecipientEmail: "integration.adopter@pawconnect.local",
            RelatedEntityType: "AdoptionRequest",
            RelatedEntityId: "sql-test",
            MaxAttempts: maxAttempts);
    }

    private static NotificationOutboxProcessor CreateOutboxProcessor(
        IDbContextFactory<ApplicationDbContext> factory,
        IntegrationTestEmailService emailService)
    {
        return new NotificationOutboxProcessor(
            factory,
            emailService,
            NullLogger<NotificationOutboxProcessor>.Instance,
            new NotificationPreferenceService(factory),
            new NotificationDeliveryLogService(factory));
    }
}
