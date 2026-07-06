using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class DogTransferServiceTests
{
    [Fact]
    public async Task CreateTransferRequestAsync_AllowsShelterToRequestTransferForOwnDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Transfer Candidate", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);

        var transfer = await service.CreateTransferRequestAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            new DogTransferCreateRequest(
                dog.Id,
                TestDbContextFactory.OtherShelterId,
                DogTransferPriority.High,
                "Shelter is overcrowded and needs short-term support."));

        Assert.Equal(DogTransferStatus.Pending, transfer.Status);
        Assert.Equal(TestDbContextFactory.OtherShelterId, transfer.DestinationShelterId);
        Assert.True(await context.DogTransferRequests.AnyAsync(request => request.DogId == dog.Id));
    }

    [Fact]
    public async Task CreateTransferRequestAsync_BlocksDogOwnedByAnotherShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Other Shelter Dog", TestDbContextFactory.OtherShelterId);
        var service = new DogTransferService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTransferRequestAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                new DogTransferCreateRequest(
                    dog.Id,
                    TestDbContextFactory.OtherShelterId,
                    DogTransferPriority.Normal,
                    "Trying to transfer a dog outside this shelter.")));

        Assert.Equal("Dog was not found for your shelter.", exception.Message);
    }

    [Fact]
    public async Task CreateTransferRequestAsync_BlocksSameSourceAndDestinationShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Same Shelter Dog", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTransferRequestAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                new DogTransferCreateRequest(
                    dog.Id,
                    TestDbContextFactory.ShelterId,
                    DogTransferPriority.Normal,
                    "Destination should not be the same shelter.")));

        Assert.Equal("Source and destination shelters cannot be the same.", exception.Message);
    }

    [Fact]
    public async Task CreateTransferRequestAsync_BlocksDuplicateActiveTransfer()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Duplicate Transfer Dog", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);

        await service.CreateTransferRequestAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            new DogTransferCreateRequest(
                dog.Id,
                TestDbContextFactory.OtherShelterId,
                DogTransferPriority.Normal,
                "First active request."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTransferRequestAsync(
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                new DogTransferCreateRequest(
                    dog.Id,
                    TestDbContextFactory.OtherShelterId,
                    DogTransferPriority.Normal,
                    "Second active request.")));

        Assert.Equal("This dog already has an active transfer request.", exception.Message);
    }

    [Fact]
    public async Task ApproveTransferAsync_AllowsDestinationShelterToApproveIncomingRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Incoming Dog", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);
        var transfer = await CreatePendingTransferAsync(service, dog.Id);

        var approved = await service.ApproveTransferAsync(
            transfer.Id,
            TestDbContextFactory.OtherShelterId,
            TestDbContextFactory.OtherShelterUserId,
            new DogTransferDecisionRequest("We have available kennel space."));

        Assert.Equal(DogTransferStatus.Approved, approved.Status);
        Assert.NotNull(approved.RespondedAtUtc);
    }

    [Fact]
    public async Task RejectTransferAsync_BlocksSourceShelterFromRejectingIncomingDecision()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Decision Dog", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);
        var transfer = await CreatePendingTransferAsync(service, dog.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectTransferAsync(
                transfer.Id,
                TestDbContextFactory.ShelterId,
                TestDbContextFactory.ShelterUserId,
                new DogTransferDecisionRequest("Wrong shelter is responding.")));

        Assert.Equal("Only the destination shelter can respond to this transfer request.", exception.Message);
    }

    [Fact]
    public async Task CancelTransferAsync_AllowsSourceShelterToCancelPendingRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Cancel Transfer Dog", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);
        var transfer = await CreatePendingTransferAsync(service, dog.Id);

        var cancelled = await service.CancelTransferAsync(
            transfer.Id,
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId);

        Assert.Equal(DogTransferStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CancelledAtUtc);
    }

    [Fact]
    public async Task CompleteTransferAsync_MovesDogToDestinationShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Completed Transfer Dog", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);
        var transfer = await CreatePendingTransferAsync(service, dog.Id);
        await service.ApproveTransferAsync(
            transfer.Id,
            TestDbContextFactory.OtherShelterId,
            TestDbContextFactory.OtherShelterUserId,
            new DogTransferDecisionRequest());

        var completed = await service.CompleteTransferAsync(
            transfer.Id,
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId);

        Assert.Equal(DogTransferStatus.Completed, completed.Status);
        Assert.Equal(
            TestDbContextFactory.OtherShelterId,
            await context.Dogs.Where(d => d.Id == dog.Id).Select(d => d.ShelterId).SingleAsync());
    }

    [Fact]
    public async Task GetTransferDetailsAsync_HidesUnrelatedShelterTransfers()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var thirdShelter = new Shelter
        {
            Id = 3,
            Name = "Third Shelter",
            Address = "Third Street",
            City = "Brasov",
            Email = "third-shelter@test.com",
            PhoneNumber = "789"
        };
        context.Shelters.Add(thirdShelter);
        var dog = await AddDogAsync(context, "Private Transfer Dog", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);
        var transfer = await CreatePendingTransferAsync(service, dog.Id);

        var hidden = await service.GetTransferDetailsAsync(transfer.Id, thirdShelter.Id);

        Assert.Null(hidden);
    }

    [Fact]
    public async Task GetAdminTransfersAsync_ReturnsTransfersAcrossShelters()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = await AddDogAsync(context, "Admin Visible Dog", TestDbContextFactory.ShelterId);
        var service = new DogTransferService(context);
        await CreatePendingTransferAsync(service, dog.Id);

        var transfers = await service.GetAdminTransfersAsync();

        Assert.Single(transfers);
        Assert.True(transfers[0].CanCancel);
    }

    private static async Task<Dog> AddDogAsync(ApplicationDbContext context, string name, int shelterId)
    {
        var dog = TestDbContextFactory.CreateDog(name, shelterId: shelterId);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        return dog;
    }

    private static Task<DogTransferRequestDto> CreatePendingTransferAsync(DogTransferService service, int dogId)
    {
        return service.CreateTransferRequestAsync(
            TestDbContextFactory.ShelterId,
            TestDbContextFactory.ShelterUserId,
            new DogTransferCreateRequest(
                dogId,
                TestDbContextFactory.OtherShelterId,
                DogTransferPriority.Normal,
                "Requesting transfer support."));
    }
}
