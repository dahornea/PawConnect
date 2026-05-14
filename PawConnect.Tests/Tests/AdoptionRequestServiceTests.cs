using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class AdoptionRequestServiceTests
{
    [Fact]
    public async Task CreateRequestAsync_SavesQuestionnaireFieldsForAvailableDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Request Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.CreateRequestAsync(TestDbContextFactory.AdopterId, dog.Id, new AdoptionRequestQuestionnaire(
            "I have time and space for this dog.",
            4,
            "I work from home.",
            FutureVisit()));

        var request = await context.AdoptionRequests.SingleAsync();
        Assert.Equal(AdoptionRequestStatus.Pending, request.Status);
        Assert.Equal(AdoptionVisitStatus.Requested, request.VisitStatus);
        Assert.NotNull(request.PreferredVisitDateTime);
        Assert.Equal("I have time and space for this dog.", request.ReasonForAdoption);
        Assert.Equal(4, request.HoursAlonePerDay);
        Assert.Equal("I work from home.", request.AdditionalInformation);
    }

    [Theory]
    [InlineData(TestDbContextFactory.ShelterUserId)]
    [InlineData(TestDbContextFactory.AdminId)]
    public async Task CreateRequestAsync_BlocksNonAdopterUsers(string userId)
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Role Protected Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(userId, dog.Id, new AdoptionRequestQuestionnaire("Good reason", null, null)));

        Assert.Equal("Only adopter accounts can submit adoption requests.", exception.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_BlocksDuplicatePendingRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Duplicate Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.Add(new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            ReasonForAdoption = "Existing pending request.",
            Status = AdoptionRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(TestDbContextFactory.AdopterId, dog.Id, new AdoptionRequestQuestionnaire("Another reason", null, null)));

        Assert.Equal("You already have a pending request for this dog.", exception.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_BlocksAdoptedDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Adopted Request Dog", DogStatus.Adopted);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(TestDbContextFactory.AdopterId, dog.Id, new AdoptionRequestQuestionnaire("Good reason", null, null)));

        Assert.Equal("Adoption requests can only be submitted for available or reserved dogs.", exception.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_BlocksPastVisitTime()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Past Visit Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(TestDbContextFactory.AdopterId, dog.Id, new AdoptionRequestQuestionnaire(
                "I have time and space for this dog.",
                4,
                null,
                DateTime.Now.AddHours(-1))));

        Assert.Equal("Please choose a future visit time.", exception.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_BlocksVisitOutsideShelterHours()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Late Visit Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(TestDbContextFactory.AdopterId, dog.Id, new AdoptionRequestQuestionnaire(
                "I have time and space for this dog.",
                4,
                null,
                NextWeekdayAt(20, 0))));

        Assert.Equal("Please choose a time within the shelter's visiting hours.", exception.Message);
    }

    [Fact]
    public async Task ConfirmVisitAsync_UpdatesRequestDogStatusAndStatusHistory()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        await service.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        var updated = await context.AdoptionRequests.Include(r => r.Dog).SingleAsync(r => r.Id == request.Id);
        Assert.Equal(AdoptionRequestStatus.VisitConfirmed, updated.Status);
        Assert.Equal(AdoptionVisitStatus.Confirmed, updated.VisitStatus);
        Assert.Equal(DogStatus.Reserved, updated.Dog!.Status);
        var history = await context.DogStatusHistories.SingleAsync();
        Assert.Equal(DogStatus.Available, history.OldStatus);
        Assert.Equal(DogStatus.Reserved, history.NewStatus);
    }

    [Fact]
    public async Task ConfirmVisitAsync_DoesNotCreateHistoryWhenStatusDoesNotChange()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Reserved);
        var service = CreateService(context);

        await service.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        Assert.False(await context.DogStatusHistories.AnyAsync());
    }

    [Fact]
    public async Task RejectRequestAsync_UpdatesPendingRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        await service.RejectRequestAsync(request.Id, TestDbContextFactory.ShelterId);

        Assert.Equal(AdoptionRequestStatus.Rejected, (await context.AdoptionRequests.FindAsync(request.Id))!.Status);
    }

    [Fact]
    public async Task RejectRequestAsync_AfterConfirmedVisitReturnsReservedDogToAvailable()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        await service.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);
        await service.RejectRequestAsync(request.Id, TestDbContextFactory.ShelterId);

        var updated = await context.AdoptionRequests.Include(r => r.Dog).SingleAsync(r => r.Id == request.Id);
        Assert.Equal(AdoptionRequestStatus.Rejected, updated.Status);
        Assert.Equal(AdoptionVisitStatus.Cancelled, updated.VisitStatus);
        Assert.Equal(DogStatus.Available, updated.Dog!.Status);
        Assert.Contains(await context.DogStatusHistories.ToListAsync(), history =>
            history.OldStatus == DogStatus.Reserved && history.NewStatus == DogStatus.Available);
    }

    [Fact]
    public async Task ShelterCannotManageAnotherSheltersRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmVisitAsync(request.Id, TestDbContextFactory.OtherShelterId));

        Assert.Equal("You cannot manage requests for another shelter's dog.", exception.Message);
    }

    [Fact]
    public async Task ConfirmVisitAsync_BlocksNonPendingRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        request.Status = AdoptionRequestStatus.Rejected;
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId));

        Assert.Equal("Only pending requests can be confirmed for a visit.", exception.Message);
    }

    [Fact]
    public async Task ConfirmVisitAsync_BlocksAdoptedDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Adopted);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId));

        Assert.Equal("This dog has already been adopted.", exception.Message);
    }

    [Fact]
    public async Task MarkAsAdoptedAsync_AfterConfirmedVisitUpdatesDogStatus()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        await service.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);
        await service.MarkAsAdoptedAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        var updated = await context.AdoptionRequests.Include(r => r.Dog).SingleAsync(r => r.Id == request.Id);
        Assert.Equal(AdoptionRequestStatus.Accepted, updated.Status);
        Assert.Equal(AdoptionVisitStatus.Completed, updated.VisitStatus);
        Assert.Equal(DogStatus.Adopted, updated.Dog!.Status);
        Assert.NotNull(updated.Dog.AdoptedAt);
    }

    [Fact]
    public async Task ConfirmVisitAsync_SendsCalendarInviteAttachment()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService);

        await service.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        var email = Assert.Single(emailService.SentEmails, sent => sent.Subject == "Your PawConnect shelter visit has been confirmed");
        var attachment = Assert.Single(email.Attachments!);
        Assert.Equal("text/calendar", attachment.ContentType);
        Assert.True(attachment.IsCalendarInvite);
        Assert.Equal("REQUEST", attachment.CalendarMethod);
        var ics = System.Text.Encoding.UTF8.GetString(attachment.Content);
        Assert.Contains("METHOD:REQUEST", ics);
        Assert.Contains("BEGIN:VEVENT", ics);
        Assert.Contains("SUMMARY:Visit Pending Dog at Test Shelter", ics);
        Assert.Contains($"UID:pawconnect-adoption-visit-{request.Id}@pawconnect.local", ics);
        Assert.Contains("BEGIN:VTIMEZONE", ics);
        Assert.Contains("TZID:Europe/Bucharest", ics);
        Assert.Contains("DTSTART;TZID=Europe/Bucharest:", ics);
        Assert.Contains("DTEND;TZID=Europe/Bucharest:", ics);
        Assert.Contains("LOCATION:Shelter Street 1\\, Bucharest", ics);
        Assert.Contains("DESCRIPTION:Adoption visit for Pending Dog\\nShelter: Test Shelter", ics);
        Assert.Contains($"Visit time: {VisitSchedulingHelper.FormatVisitDateTime(request.PreferredVisitDateTime)}", ics);
        Assert.Contains("Address: Shelter Street 1\\, Bucharest", ics);
        Assert.Contains("Email: shelter@test.com", ics);
        Assert.Contains("Phone: 123", ics);
        Assert.Contains("If you cannot attend\\, please contact the shelter.", ics);
        Assert.Contains("STATUS:CONFIRMED", ics);
        Assert.Contains("ORGANIZER;", ics);
        Assert.Contains("ATTENDEE;", ics);
    }

    [Fact]
    public async Task ConfirmVisitAsync_UsesBucharestLocalTimeInCalendarInvite()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        request.PreferredVisitDateTime = new DateTime(2026, 5, 15, 10, 0, 0);
        await context.SaveChangesAsync();
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService);

        await service.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        var email = Assert.Single(emailService.SentEmails, sent => sent.Subject == "Your PawConnect shelter visit has been confirmed");
        Assert.Contains("15 May 2026 10:00", email.Body);
        var attachment = Assert.Single(email.Attachments!);
        var ics = System.Text.Encoding.UTF8.GetString(attachment.Content);
        Assert.Contains("DTSTART;TZID=Europe/Bucharest:20260515T100000", ics);
        Assert.Contains("DTEND;TZID=Europe/Bucharest:20260515T110000", ics);
        Assert.DoesNotContain("DTSTART:20260515T070000Z", ics);
        Assert.Contains("METHOD:REQUEST", ics);
        Assert.Contains($"UID:pawconnect-adoption-visit-{request.Id}@pawconnect.local", ics);
        Assert.Contains("DTSTAMP:", ics);
        Assert.Contains("SUMMARY:Visit Pending Dog at Test Shelter", ics);
        Assert.Contains("LOCATION:Shelter Street 1\\, Bucharest", ics);
        Assert.Contains("DESCRIPTION:", ics);
        Assert.Contains("ORGANIZER;", ics);
        Assert.Contains("ATTENDEE;", ics);
        Assert.Contains("STATUS:CONFIRMED", ics);
    }

    [Fact]
    public async Task CancelRequestAsync_OnlyCancelsPendingOwnRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        await service.CancelRequestAsync(request.Id, TestDbContextFactory.AdopterId);

        Assert.Equal(AdoptionRequestStatus.Cancelled, (await context.AdoptionRequests.FindAsync(request.Id))!.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelRequestAsync(request.Id, TestDbContextFactory.AdopterId));
    }

    [Fact]
    public async Task CancelRequestAsync_BlocksAnotherAdopter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelRequestAsync(request.Id, TestDbContextFactory.SecondAdopterId));

        Assert.Equal("You can only cancel your own adoption requests.", exception.Message);
        Assert.Equal(AdoptionRequestStatus.Pending, (await context.AdoptionRequests.FindAsync(request.Id))!.Status);
    }

    private static AdoptionRequestService CreateService(ApplicationDbContext context, TestEmailService? emailService = null)
    {
        return new AdoptionRequestService(
            context,
            emailService ?? new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<AdoptionRequestService>.Instance,
            TestDbContextFactory.CreateUserManager(context));
    }

    private static async Task<AdoptionRequest> SeedPendingRequestAsync(ApplicationDbContext context, DogStatus dogStatus)
    {
        var dog = TestDbContextFactory.CreateDog("Pending Dog", dogStatus);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            ReasonForAdoption = "I am ready to adopt.",
            Status = AdoptionRequestStatus.Pending,
            PreferredVisitDateTime = FutureVisit(),
            VisitStatus = AdoptionVisitStatus.Requested,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AdoptionRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    private static DateTime FutureVisit()
    {
        return NextWeekdayAt(11, 0);
    }

    private static DateTime NextWeekdayAt(int hour, int minute)
    {
        var date = DateTime.Today.AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date.AddHours(hour).AddMinutes(minute);
    }
}
