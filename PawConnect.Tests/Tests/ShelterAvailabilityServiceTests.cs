using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class ShelterAvailabilityServiceTests
{
    [Fact]
    public async Task CreateSlotAsync_AllowsShelterToCreateOwnFutureSlot()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = CreateService(databaseName);

        var slot = await service.CreateSlotAsync(
            new CreateShelterAvailabilitySlotRequest(
                TestDbContextFactory.ShelterId,
                NextWeekdayAt(10, 0),
                NextWeekdayAt(11, 0),
                "Morning visits"),
            TestDbContextFactory.ShelterUserId);

        Assert.Equal(TestDbContextFactory.ShelterId, slot.ShelterId);
        Assert.False(slot.IsBooked);
        Assert.False(slot.IsCancelled);
        Assert.Equal("Morning visits", slot.Notes);
    }

    [Fact]
    public async Task CreateSlotAsync_BlocksAnotherSheltersSlot()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = CreateService(databaseName);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSlotAsync(
                new CreateShelterAvailabilitySlotRequest(
                    TestDbContextFactory.OtherShelterId,
                    NextWeekdayAt(10, 0),
                    NextWeekdayAt(11, 0)),
                TestDbContextFactory.ShelterUserId));

        Assert.Equal("You cannot manage availability for another shelter.", exception.Message);
    }

    [Fact]
    public async Task CreateSlotAsync_RejectsOverlappingSlot()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = CreateService(databaseName);

        await service.CreateSlotAsync(
            new CreateShelterAvailabilitySlotRequest(
                TestDbContextFactory.ShelterId,
                NextWeekdayAt(10, 0),
                NextWeekdayAt(11, 0)),
            TestDbContextFactory.ShelterUserId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSlotAsync(
                new CreateShelterAvailabilitySlotRequest(
                    TestDbContextFactory.ShelterId,
                    NextWeekdayAt(10, 30),
                    NextWeekdayAt(11, 30)),
                TestDbContextFactory.ShelterUserId));

        Assert.Equal("This slot overlaps an existing active availability slot.", exception.Message);
    }

    [Fact]
    public async Task GetAvailableSlotsForAdoptionRequestAsync_BlocksAnotherAdopter()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var request = await SeedPendingRequestAsync(context);
        var service = CreateService(databaseName);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAvailableSlotsForAdoptionRequestAsync(request.Id, TestDbContextFactory.SecondAdopterId));

        Assert.Equal("You cannot view slots for this adoption request.", exception.Message);
    }

    [Fact]
    public async Task BookSlotForAdoptionRequestAsync_SetsVisitTimeAndBlocksReuse()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var firstRequest = await SeedPendingRequestAsync(context, "First Slot Dog");
        var secondRequest = await SeedPendingRequestAsync(context, "Second Slot Dog");
        var service = CreateService(databaseName);
        var slot = await service.CreateSlotAsync(
            new CreateShelterAvailabilitySlotRequest(
                TestDbContextFactory.ShelterId,
                NextWeekdayAt(12, 0),
                NextWeekdayAt(13, 0)),
            TestDbContextFactory.ShelterUserId);

        await service.BookSlotForAdoptionRequestAsync(firstRequest.Id, slot.Id, TestDbContextFactory.ShelterUserId);

        context.ChangeTracker.Clear();
        var updated = await context.AdoptionRequests.FindAsync(firstRequest.Id);
        Assert.Equal(NextWeekdayAt(12, 0), updated!.PreferredVisitDateTime);
        Assert.True((await context.ShelterAvailabilitySlots.FindAsync(slot.Id))!.IsBooked);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BookSlotForAdoptionRequestAsync(secondRequest.Id, slot.Id, TestDbContextFactory.ShelterUserId));

        Assert.Equal("This slot is already booked.", exception.Message);
    }

    private static ShelterAvailabilityService CreateService(string databaseName)
    {
        return new ShelterAvailabilityService(TestDbContextFactory.CreateContextFactory(databaseName));
    }

    private static async Task<AdoptionRequest> SeedPendingRequestAsync(ApplicationDbContext context, string dogName = "Slot Dog")
    {
        var dog = TestDbContextFactory.CreateDog(dogName, DogStatus.Available);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            ReasonForAdoption = "I am ready to adopt.",
            Status = AdoptionRequestStatus.Pending,
            VisitStatus = AdoptionVisitStatus.Requested,
            PreferredVisitDateTime = NextWeekdayAt(10, 0),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AdoptionRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
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
