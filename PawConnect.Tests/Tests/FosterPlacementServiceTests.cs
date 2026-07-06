using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class FosterPlacementServiceTests
{
    [Fact]
    public async Task CreatePlacementAsync_AllowsShelterToAssignOwnDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Foster Candidate", TestDbContextFactory.ShelterId);
        var caregiver = await AddCaregiverAsync(context, "Caregiver One", TestDbContextFactory.ShelterId);
        var service = new FosterPlacementService(context);

        var placement = await service.CreatePlacementAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest(dog.Id, caregiver.Id));

        Assert.Equal(FosterPlacementStatus.Pending, placement.Status);
        Assert.Equal(dog.Id, placement.DogId);
        Assert.True(await context.FosterPlacements.AnyAsync(item => item.DogId == dog.Id));
        Assert.True(await context.FosterPlacementActivities.AnyAsync(activity => activity.ActivityType == FosterPlacementActivityType.Created));
    }

    [Fact]
    public async Task CreatePlacementAsync_BlocksDogOwnedByAnotherShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Other Shelter Foster Dog", TestDbContextFactory.OtherShelterId);
        var caregiver = await AddCaregiverAsync(context, "Caregiver One", TestDbContextFactory.ShelterId);
        var service = new FosterPlacementService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlacementAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                CreateRequest(dog.Id, caregiver.Id)));

        Assert.Equal("Dog was not found for your shelter.", exception.Message);
    }

    [Fact]
    public async Task CreatePlacementAsync_BlocksDuplicateOpenPlacementForDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Duplicate Foster Dog", TestDbContextFactory.ShelterId);
        var caregiver = await AddCaregiverAsync(context, "Caregiver One", TestDbContextFactory.ShelterId, capacity: 2);
        var service = new FosterPlacementService(context);

        await service.CreatePlacementAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest(dog.Id, caregiver.Id));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlacementAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                CreateRequest(dog.Id, caregiver.Id)));

        Assert.Equal("This dog already has an open foster placement.", exception.Message);
    }

    [Fact]
    public async Task CreatePlacementAsync_BlocksInactiveCaregiver()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Inactive Caregiver Dog", TestDbContextFactory.ShelterId);
        var caregiver = await AddCaregiverAsync(context, "Inactive Caregiver", TestDbContextFactory.ShelterId, isActive: false);
        var service = new FosterPlacementService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlacementAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                CreateRequest(dog.Id, caregiver.Id)));

        Assert.Equal("Foster caregiver must be active before assignment.", exception.Message);
    }

    [Fact]
    public async Task CreatePlacementAsync_ValidatesPlannedEndAfterStart()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Invalid Foster Dates", TestDbContextFactory.ShelterId);
        var caregiver = await AddCaregiverAsync(context, "Caregiver One", TestDbContextFactory.ShelterId);
        var service = new FosterPlacementService(context);
        var startDate = DateTime.UtcNow.Date.AddDays(4);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlacementAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                CreateRequest(dog.Id, caregiver.Id, startDate, startDate.AddDays(-1))));

        Assert.Equal("Planned end date must be after the start date.", exception.Message);
    }

    [Fact]
    public async Task ApproveStartAndCompletePlacement_UpdatesStatusCapacityAndActivityHistory()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Foster Lifecycle Dog", TestDbContextFactory.ShelterId);
        var caregiver = await AddCaregiverAsync(context, "Caregiver One", TestDbContextFactory.ShelterId);
        var service = new FosterPlacementService(context);
        var startDate = DateTime.UtcNow.Date.AddDays(1);
        var created = await service.CreatePlacementAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest(dog.Id, caregiver.Id, startDate, startDate.AddDays(10)));

        await service.ApprovePlacementAsync(
            created.Id,
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            new FosterPlacementDecisionRequest("Caregiver confirmed."));
        var active = await service.StartPlacementAsync(
            created.Id,
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            new FosterPlacementStartRequest("Dog moved today."));

        Assert.Equal(FosterPlacementStatus.Active, active.Status);
        Assert.Equal(1, await context.FosterCaregiverProfiles.Where(item => item.Id == caregiver.Id).Select(item => item.ActivePlacementCount).SingleAsync());

        var completed = await service.CompletePlacementAsync(
            created.Id,
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            isAdmin: false,
            new FosterPlacementCompleteRequest(startDate.AddDays(7), "Returned to shelter care."));

        Assert.Equal(FosterPlacementStatus.Completed, completed.Status);
        Assert.Equal(0, await context.FosterCaregiverProfiles.Where(item => item.Id == caregiver.Id).Select(item => item.ActivePlacementCount).SingleAsync());
        var activityTypes = await context.FosterPlacementActivities
            .Where(activity => activity.FosterPlacementId == created.Id)
            .Select(activity => activity.ActivityType)
            .ToListAsync();
        Assert.Contains(FosterPlacementActivityType.Created, activityTypes);
        Assert.Contains(FosterPlacementActivityType.Approved, activityTypes);
        Assert.Contains(FosterPlacementActivityType.Started, activityTypes);
        Assert.Contains(FosterPlacementActivityType.Completed, activityTypes);
    }

    [Fact]
    public async Task GetPlacementDetailsAsync_HidesPlacementFromUnrelatedShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Private Foster Dog", TestDbContextFactory.ShelterId);
        var caregiver = await AddCaregiverAsync(context, "Caregiver One", TestDbContextFactory.ShelterId);
        var service = new FosterPlacementService(context);
        var created = await service.CreatePlacementAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            CreateRequest(dog.Id, caregiver.Id));

        var hidden = await service.GetPlacementDetailsAsync(created.Id, TestDbContextFactory.OtherShelterId);
        var visible = await service.GetPlacementDetailsAsync(created.Id, TestDbContextFactory.ShelterId);

        Assert.Null(hidden);
        Assert.NotNull(visible);
    }

    [Fact]
    public async Task GetAdminPlacementsAsync_ReturnsPlacementsAcrossShelters()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var shelterDog = await AddDogAsync(context, "Shelter Foster Dog", TestDbContextFactory.ShelterId);
        var otherDog = await AddDogAsync(context, "Other Foster Dog", TestDbContextFactory.OtherShelterId);
        var shelterCaregiver = await AddCaregiverAsync(context, "Caregiver One", TestDbContextFactory.ShelterId);
        var otherCaregiver = await AddCaregiverAsync(context, "Caregiver Two", TestDbContextFactory.OtherShelterId);
        var service = new FosterPlacementService(context);
        await service.CreatePlacementAsync(TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId, CreateRequest(shelterDog.Id, shelterCaregiver.Id));
        await service.CreatePlacementAsync(TestDbContextFactory.OtherShelterId, TestDbContextFactory.OtherShelterUserId, CreateRequest(otherDog.Id, otherCaregiver.Id));

        var placements = await service.GetAdminPlacementsAsync();

        Assert.Equal(2, placements.Count);
        Assert.Contains(placements, placement => placement.ShelterId == TestDbContextFactory.ShelterId);
        Assert.Contains(placements, placement => placement.ShelterId == TestDbContextFactory.OtherShelterId);
    }

    private static FosterPlacementCreateRequest CreateRequest(
        int dogId,
        int caregiverId,
        DateTime? startDate = null,
        DateTime? plannedEndDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.Date.AddDays(1);
        return new FosterPlacementCreateRequest(
            dogId,
            caregiverId,
            FosterPlacementPriority.Normal,
            FosterPlacementReason.Overcrowding,
            start,
            plannedEndDate ?? start.AddDays(14),
            "Keep routine calm and record appetite changes.",
            "Vaccinations checked before foster placement.",
            "Shelter will call the caregiver after the first night.");
    }

    private static async Task<Dog> AddDogAsync(ApplicationDbContext context, string name, int shelterId)
    {
        var dog = TestDbContextFactory.CreateDog(name, shelterId: shelterId);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        return dog;
    }

    private static async Task<FosterCaregiverProfile> AddCaregiverAsync(
        ApplicationDbContext context,
        string name,
        int shelterId,
        int capacity = 1,
        bool isActive = true)
    {
        var caregiver = new FosterCaregiverProfile
        {
            DisplayName = name,
            Email = $"{name.Replace(" ", ".").ToLowerInvariant()}@foster.test",
            PreferredShelterId = shelterId,
            Capacity = capacity,
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        context.FosterCaregiverProfiles.Add(caregiver);
        await context.SaveChangesAsync();
        return caregiver;
    }
}