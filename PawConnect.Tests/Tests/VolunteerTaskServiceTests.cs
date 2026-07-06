using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class VolunteerTaskServiceTests
{
    private const string VolunteerUserId = "volunteer-test-id";
    private const string OtherVolunteerUserId = "other-volunteer-test-id";

    [Fact]
    public async Task CreateTaskAsync_AllowsShelterToCreateTaskForOwnDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Volunteer Dog", TestDbContextFactory.ShelterId);
        var service = new VolunteerTaskService(context);

        var created = await service.CreateTaskAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest(dog.Id));

        Assert.Equal("Morning walk support", created.Title);
        Assert.Equal(TestDbContextFactory.ShelterId, created.ShelterId);
        Assert.Equal(dog.Id, created.DogId);
        Assert.Equal(VolunteerTaskStatus.Open, created.Status);
        Assert.True(await context.VolunteerTaskActivities.AnyAsync(activity =>
            activity.VolunteerTaskId == created.Id &&
            activity.ActivityType == VolunteerTaskActivityType.Created));
    }

    [Fact]
    public async Task CreateTaskAsync_BlocksInvalidSchedule()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new VolunteerTaskService(context);
        var start = DateTime.UtcNow.AddHours(2);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTaskAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                CreateRequest(startUtc: start, endUtc: start.AddMinutes(-10))));

        Assert.Contains("end time", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignTaskAsync_BlocksOverlappingActiveAssignments()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var volunteer = await AddVolunteerAsync(context, VolunteerUserId, TestDbContextFactory.ShelterId);
        var service = new VolunteerTaskService(context);
        var start = DateTime.UtcNow.AddDays(1).AddHours(9);
        var first = await service.CreateTaskAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest(startUtc: start, endUtc: start.AddHours(1), assignedVolunteerProfileId: volunteer.Id));
        var second = await service.CreateTaskAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest(startUtc: start.AddMinutes(30), endUtc: start.AddHours(2)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignTaskAsync(
                second.Id,
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                new VolunteerTaskAssignRequest(volunteer.Id)));

        Assert.Contains("assigned task", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(VolunteerTaskStatus.Assigned, first.Status);
    }

    [Fact]
    public async Task VolunteerCanAcceptStartAndCompleteOpenTask()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await AddVolunteerAsync(context, VolunteerUserId, TestDbContextFactory.ShelterId);
        var service = new VolunteerTaskService(context);
        var task = await service.CreateTaskAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest());

        var accepted = await service.AcceptTaskAsync(task.Id, VolunteerUserId);
        var started = await service.StartTaskAsync(task.Id, VolunteerUserId);
        var completed = await service.CompleteTaskAsync(
            task.Id,
            VolunteerUserId,
            new VolunteerTaskActionRequest("Walk completed without issues."));

        Assert.Equal(VolunteerTaskStatus.Assigned, accepted.Status);
        Assert.Equal(VolunteerTaskStatus.InProgress, started.Status);
        Assert.Equal(VolunteerTaskStatus.Completed, completed.Status);
        Assert.Contains(completed.Activities, activity => activity.ActivityType == VolunteerTaskActivityType.Completed);
    }

    [Fact]
    public async Task VolunteerCannotAcceptTaskForDifferentPreferredShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await AddVolunteerAsync(context, VolunteerUserId, TestDbContextFactory.OtherShelterId);
        var service = new VolunteerTaskService(context);
        var task = await service.CreateTaskAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcceptTaskAsync(task.Id, VolunteerUserId));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTaskDetailsAsync_HidesTaskFromUnrelatedShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new VolunteerTaskService(context);
        var task = await service.CreateTaskAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest());

        var hidden = await service.GetTaskDetailsAsync(task.Id, shelterId: TestDbContextFactory.OtherShelterId);

        Assert.Null(hidden);
    }

    private static VolunteerTaskCreateRequest CreateRequest(
        int? dogId = null,
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        int? assignedVolunteerProfileId = null)
    {
        var start = startUtc ?? DateTime.UtcNow.AddDays(1).AddHours(8);
        return new VolunteerTaskCreateRequest(
            "Morning walk support",
            "Help with a calm morning walk.",
            VolunteerTaskCategory.DogWalking,
            VolunteerTaskPriority.Normal,
            start,
            endUtc ?? start.AddHours(1),
            start.AddHours(1),
            dogId,
            assignedVolunteerProfileId,
            "Quiet walking route",
            "Comfortable with dogs",
            "Use the green harness.");
    }

    private static async Task<Dog> AddDogAsync(ApplicationDbContext context, string name, int shelterId)
    {
        var dog = TestDbContextFactory.CreateDog(name, shelterId: shelterId);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        return dog;
    }

    private static async Task<VolunteerProfile> AddVolunteerAsync(ApplicationDbContext context, string userId, int? preferredShelterId)
    {
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            NormalizedUserName = $"{userId}@test.com".ToUpperInvariant(),
            Email = $"{userId}@test.com",
            NormalizedEmail = $"{userId}@test.com".ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = userId == VolunteerUserId ? "Test Volunteer" : "Other Volunteer"
        });

        var profile = new VolunteerProfile
        {
            UserId = userId,
            DisplayName = userId == VolunteerUserId ? "Test Volunteer" : "Other Volunteer",
            Email = $"{userId}@test.com",
            PreferredShelterId = preferredShelterId,
            Skills = "Dog walking",
            AvailabilityNotes = "Weekday mornings",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        context.VolunteerProfiles.Add(profile);
        await context.SaveChangesAsync();
        return profile;
    }
}
