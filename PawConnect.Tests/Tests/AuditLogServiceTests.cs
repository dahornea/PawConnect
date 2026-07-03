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
    public async Task ConfirmVisitAsync_WritesAuditLog()
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
            PreferredVisitDateTime = FutureVisit(),
            VisitStatus = AdoptionVisitStatus.Requested,
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

        await adoptionService.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        Assert.True(await context.AuditLogs.AnyAsync(log =>
            log.Action == AuditActions.VisitConfirmed &&
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

    [Fact]
    public async Task LogUserActionAsync_StoresCorrelationUserAgentAndSafeDetails()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "PawConnect test browser";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var service = new AuditLogService(
            context,
            accessor,
            NullLogger<AuditLogService>.Instance,
            new TestCorrelationIdAccessor("corr-test-123"));

        await service.LogUserActionAsync(
            "SensitiveAction",
            "Dog",
            "42",
            "Sensitive action was tested.",
            new { DogId = 42, Password = "secret-value", SafeValue = "visible" });

        var log = await context.AuditLogs.SingleAsync();
        Assert.Equal("corr-test-123", log.CorrelationId);
        Assert.Equal("PawConnect test browser", log.UserAgent);
        Assert.Contains("visible", log.DetailsJson);
        Assert.Contains("[redacted]", log.DetailsJson);
        Assert.DoesNotContain("secret-value", log.DetailsJson);
    }

    [Fact]
    public async Task LogAsync_FailureDoesNotThrowIntoCaller()
    {
        var context = TestDbContextFactory.CreateContext();
        var service = CreateAuditService(context);
        await context.DisposeAsync();

        var exception = await Record.ExceptionAsync(() => service.LogAsync(
            AuditActions.DogCreated,
            "Dog",
            "1",
            "This should not escape."));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_PreservesIncomingCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers[CorrelationIdAccessor.HeaderName] = "demo-correlation-1";
        var middleware = new CorrelationIdMiddleware(
            _ => context.Response.StartAsync(),
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("demo-correlation-1", context.Items[CorrelationIdAccessor.ItemsKey]);
        Assert.Equal("demo-correlation-1", context.Response.Headers[CorrelationIdAccessor.HeaderName]);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_GeneratesCorrelationIdWhenMissing()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(
            _ => context.Response.StartAsync(),
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var correlationId = Assert.IsType<string>(context.Items[CorrelationIdAccessor.ItemsKey]);
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdAccessor.HeaderName]);
    }

    private static AuditLogService CreateAuditService(ApplicationDbContext context)
    {
        return new AuditLogService(
            context,
            new HttpContextAccessor(),
            NullLogger<AuditLogService>.Instance);
    }

    private sealed class TestCorrelationIdAccessor(string correlationId) : ICorrelationIdAccessor
    {
        public string? GetCorrelationId()
        {
            return correlationId;
        }
    }

    private static DateTime FutureVisit()
    {
        var date = DateTime.Today.AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date.AddHours(11);
    }
}
