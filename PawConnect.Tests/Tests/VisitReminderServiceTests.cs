using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class VisitReminderServiceTests
{
    [Fact]
    public async Task GetDueVisitRemindersAsync_ReturnsConfirmedVisitTwentyFourHoursAway()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var now = DateTime.Now;
        await SeedRequestAsync(context, now.AddHours(24));
        var service = CreateService(context);

        var due = await service.GetDueVisitRemindersAsync(now);

        var request = Assert.Single(due);
        Assert.Equal(AdoptionRequestStatus.VisitConfirmed, request.Status);
        Assert.Equal(AdoptionVisitStatus.Confirmed, request.VisitStatus);
    }

    [Fact]
    public async Task GetDueVisitRemindersAsync_IgnoresUnconfirmedVisit()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var now = DateTime.Now;
        await SeedRequestAsync(
            context,
            now.AddHours(24),
            AdoptionRequestStatus.Pending,
            AdoptionVisitStatus.Requested);
        var service = CreateService(context);

        var due = await service.GetDueVisitRemindersAsync(now);

        Assert.Empty(due);
    }

    [Fact]
    public async Task GetDueVisitRemindersAsync_IgnoresRejectedOrCancelledRequests()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var now = DateTime.Now;
        await SeedRequestAsync(
            context,
            now.AddHours(24),
            AdoptionRequestStatus.Rejected,
            AdoptionVisitStatus.Cancelled);
        var service = CreateService(context);

        var due = await service.GetDueVisitRemindersAsync(now);

        Assert.Empty(due);
    }

    [Fact]
    public async Task GetDueVisitRemindersAsync_IgnoresAlreadySentReminder()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var now = DateTime.Now;
        await SeedRequestAsync(context, now.AddHours(24), reminderSentAt: DateTime.UtcNow);
        var service = CreateService(context);

        var due = await service.GetDueVisitRemindersAsync(now);

        Assert.Empty(due);
    }

    [Fact]
    public async Task SendDueVisitRemindersAsync_SendsEmailWithCalendarAttachmentAndMarksSent()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var request = await SeedRequestAsync(context, DateTime.Now.AddHours(24));
        var emailService = new TestEmailService();
        var notificationService = new NotificationService(
            TestDbContextFactory.CreateContextFactory(databaseName),
            NullLogger<NotificationService>.Instance);
        var auditLogService = new AuditLogService(
            context,
            new HttpContextAccessor(),
            NullLogger<AuditLogService>.Instance);
        var service = CreateService(context, emailService, notificationService, auditLogService);

        var sentCount = await service.SendDueVisitRemindersAsync();

        Assert.Equal(1, sentCount);
        var email = Assert.Single(emailService.SentEmails);
        Assert.Equal("Reminder: your PawConnect shelter visit is tomorrow", email.Subject);
        Assert.Contains("This is a reminder that your adoption visit is scheduled for tomorrow.", email.Body);
        var attachment = Assert.Single(email.Attachments!);
        Assert.Equal("text/calendar", attachment.ContentType);
        Assert.True(attachment.IsCalendarInvite);
        var ics = System.Text.Encoding.UTF8.GetString(attachment.Content);
        Assert.Contains("METHOD:REQUEST", ics);
        Assert.Contains("BEGIN:VEVENT", ics);
        Assert.Contains($"UID:pawconnect-adoption-visit-{request.Id}@pawconnect.local", ics);
        Assert.Contains("STATUS:CONFIRMED", ics);

        context.ChangeTracker.Clear();
        var updated = await context.AdoptionRequests.SingleAsync(r => r.Id == request.Id);
        Assert.NotNull(updated.VisitReminderSentAt);
        Assert.True(await context.Notifications.AnyAsync(n =>
            n.UserId == TestDbContextFactory.AdopterId &&
            n.Title == "Visit reminder" &&
            n.Category == NotificationCategory.Adoption));
        Assert.True(await context.AuditLogs.AnyAsync(log =>
            log.Action == AuditActions.VisitReminderSent &&
            log.EntityId == request.Id.ToString()));
    }

    [Fact]
    public async Task SendVisitReminderAsync_DoesNotMarkSentWhenEmailThrows()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedRequestAsync(context, DateTime.Now.AddHours(24));
        var emailService = new TestEmailService { ThrowOnSend = true };
        var service = CreateService(context, emailService);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendVisitReminderAsync(request.Id));

        context.ChangeTracker.Clear();
        var updated = await context.AdoptionRequests.SingleAsync(r => r.Id == request.Id);
        Assert.Null(updated.VisitReminderSentAt);
    }

    private static VisitReminderService CreateService(
        ApplicationDbContext context,
        TestEmailService? emailService = null,
        INotificationService? notificationService = null,
        IAuditLogService? auditLogService = null)
    {
        return new VisitReminderService(
            context,
            emailService ?? new TestEmailService(),
            Options.Create(new VisitReminderSettings()),
            NullLogger<VisitReminderService>.Instance,
            notificationService,
            auditLogService);
    }

    private static async Task<AdoptionRequest> SeedRequestAsync(
        ApplicationDbContext context,
        DateTime visitTime,
        AdoptionRequestStatus status = AdoptionRequestStatus.VisitConfirmed,
        AdoptionVisitStatus visitStatus = AdoptionVisitStatus.Confirmed,
        DateTime? reminderSentAt = null)
    {
        var dog = TestDbContextFactory.CreateDog("Reminder Dog", DogStatus.Reserved);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            Status = status,
            VisitStatus = visitStatus,
            PreferredVisitDateTime = visitTime,
            VisitConfirmedAt = DateTime.UtcNow,
            VisitReminderSentAt = reminderSentAt,
            ReasonForAdoption = "I can attend the scheduled visit.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.AdoptionRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }
}
