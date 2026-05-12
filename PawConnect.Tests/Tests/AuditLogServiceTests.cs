using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class AuditLogServiceTests
{
    [Fact]
    public async Task GetRecentLogsAsync_ReturnsNewestFirst()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateAuditService(context);

        await service.LogAsync(new AuditLog
        {
            Action = AuditActions.DogCreated,
            EntityName = "Dog",
            EntityId = "1",
            Description = "Older dog log.",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        });
        await service.LogAsync(new AuditLog
        {
            Action = AuditActions.ResourceUpdated,
            EntityName = "ResourceStock",
            EntityId = "2",
            Description = "Newer resource log.",
            CreatedAt = DateTime.UtcNow
        });

        var logs = await service.GetRecentLogsAsync(10);

        Assert.Equal(AuditActions.ResourceUpdated, logs[0].Action);
        Assert.Equal(AuditActions.DogCreated, logs[1].Action);
    }

    [Fact]
    public async Task CreateDogAsync_WritesAuditLog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var auditService = CreateAuditService(context);
        var dogService = new DogService(context, auditService);

        await dogService.CreateDogAsync(TestDbContextFactory.CreateDog("Audit Bella"), TestDbContextFactory.ShelterId);

        var log = await context.AuditLogs.SingleAsync(log => log.Action == AuditActions.DogCreated);
        Assert.Equal("Dog", log.EntityName);
        Assert.Contains("Audit Bella", log.Description);
        Assert.DoesNotContain("PasswordHash", log.Description);
        Assert.DoesNotContain("SecurityStamp", log.Description);
    }

    [Fact]
    public async Task AcceptRequestAsync_WritesAuditLog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Audit Request Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            Status = AdoptionRequestStatus.Pending,
            ReasonForAdoption = "Ready to adopt.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AdoptionRequests.Add(request);
        await context.SaveChangesAsync();
        var auditService = CreateAuditService(context);
        var adoptionService = new AdoptionRequestService(
            context,
            new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<AdoptionRequestService>.Instance,
            TestDbContextFactory.CreateUserManager(context),
            auditLogService: auditService);

        await adoptionService.AcceptRequestAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        Assert.True(await context.AuditLogs.AnyAsync(log =>
            log.Action == AuditActions.AdoptionRequestAccepted &&
            log.EntityName == "AdoptionRequest" &&
            log.EntityId == request.Id.ToString()));
    }

    [Fact]
    public async Task UpdateResourceAsync_WritesAuditLog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var resource = new ResourceStock
        {
            ShelterId = TestDbContextFactory.ShelterId,
            Name = "Audit Food",
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            FoodTypeId = TestDbContextFactory.AdultFoodTypeId,
            Quantity = 10,
            Unit = "kg",
            LowStockThreshold = 5
        };
        context.ResourceStocks.Add(resource);
        await context.SaveChangesAsync();
        var auditService = CreateAuditService(context);
        var resourceService = new ResourceStockService(
            context,
            new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<ResourceStockService>.Instance,
            auditService);

        resource.Quantity = 8;
        await resourceService.UpdateResourceAsync(resource, TestDbContextFactory.ShelterId);

        var log = await context.AuditLogs.SingleAsync(log => log.Action == AuditActions.ResourceUpdated);
        Assert.Equal("ResourceStock", log.EntityName);
        Assert.Equal(resource.Id.ToString(), log.EntityId);
        Assert.Contains("Audit Food", log.Description);
    }

    private static AuditLogService CreateAuditService(ApplicationDbContext context)
    {
        return new AuditLogService(
            context,
            new HttpContextAccessor(),
            NullLogger<AuditLogService>.Instance);
    }
}
