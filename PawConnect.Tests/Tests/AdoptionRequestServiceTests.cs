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
            "I work from home."));

        var request = await context.AdoptionRequests.SingleAsync();
        Assert.Equal(AdoptionRequestStatus.Pending, request.Status);
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

        Assert.Equal("You already have a pending adoption request for this dog.", exception.Message);
    }

    [Fact]
    public async Task AcceptRequestAsync_UpdatesRequestDogStatusAndStatusHistory()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        await service.AcceptRequestAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

        var updated = await context.AdoptionRequests.Include(r => r.Dog).SingleAsync(r => r.Id == request.Id);
        Assert.Equal(AdoptionRequestStatus.Accepted, updated.Status);
        Assert.Equal(DogStatus.Reserved, updated.Dog!.Status);
        var history = await context.DogStatusHistories.SingleAsync();
        Assert.Equal(DogStatus.Available, history.OldStatus);
        Assert.Equal(DogStatus.Reserved, history.NewStatus);
    }

    [Fact]
    public async Task AcceptRequestAsync_DoesNotCreateHistoryWhenStatusDoesNotChange()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Reserved);
        var service = CreateService(context);

        await service.AcceptRequestAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);

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
    public async Task ShelterCannotManageAnotherSheltersRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = await SeedPendingRequestAsync(context, DogStatus.Available);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcceptRequestAsync(request.Id, TestDbContextFactory.OtherShelterId));

        Assert.Equal("This adoption request does not belong to your shelter.", exception.Message);
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

    private static AdoptionRequestService CreateService(ApplicationDbContext context)
    {
        return new AdoptionRequestService(
            context,
            new TestEmailService(),
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AdoptionRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }
}
