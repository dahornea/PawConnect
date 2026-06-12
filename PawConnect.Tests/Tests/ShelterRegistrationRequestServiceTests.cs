using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class ShelterRegistrationRequestServiceTests
{
    [Fact]
    public async Task SubmitRequestAsync_CreatesPendingRequestAndSendsAdminEmail()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService);

        var request = await service.SubmitRequestAsync(CreateRequest(latitude: null, longitude: null));

        Assert.Equal(ShelterRegistrationRequestStatus.Pending, request.Status);
        Assert.Null(request.Latitude);
        Assert.Null(request.Longitude);
        Assert.Contains(context.ShelterRegistrationRequests, item => item.Email == "new-shelter@example.test");
        var email = Assert.Single(emailService.SentEmails);
        Assert.Equal("admin@test.com", email.To);
        Assert.Contains(email.Attachments!, attachment => attachment.FileName == "ShelterRegistrationRequest.pdf");
    }

    [Fact]
    public async Task SubmitRequestAsync_BlocksDuplicatePendingRequestForSameEmail()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        await service.SubmitRequestAsync(CreateRequest(email: "New-Shelter@Example.Test"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitRequestAsync(CreateRequest(email: " new-shelter@example.test ")));
        Assert.Equal("A shelter application with this email is already pending review.", exception.Message);
    }

    [Fact]
    public async Task SubmitRequestAsync_BlocksExistingShelterAccountEmail()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitRequestAsync(CreateRequest(email: " shelter@test.com ")));

        Assert.Equal("A shelter account with this email already exists.", exception.Message);
    }

    [Fact]
    public async Task SubmitRequestAsync_RejectsInvalidCoordinates()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitRequestAsync(CreateRequest(latitude: 91, longitude: 23.6)));

        Assert.Equal("Latitude must be between -90 and 90.", exception.Message);
    }

    [Fact]
    public async Task AcceptRequestAsync_CreatesShelterUserRoleAndLinkedShelterWithCoordinates()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var userManager = TestDbContextFactory.CreateUserManager(context);
        var service = CreateService(context, userManager: userManager);
        var request = await service.SubmitRequestAsync(CreateRequest(latitude: 46.75, longitude: 23.6));

        await service.AcceptRequestAsync(request.Id, TestDbContextFactory.AdminId);

        var user = await userManager.FindByEmailAsync("new-shelter@example.test");
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, IdentitySeedData.ShelterRole));
        var shelter = context.Shelters.Single(shelter => shelter.Email == "new-shelter@example.test");
        Assert.Equal(user!.Id, shelter.ApplicationUserId);
        Assert.Equal(46.75, shelter.Latitude);
        Assert.Equal(23.6, shelter.Longitude);
        Assert.Equal("Buna Ziua", shelter.Neighborhood);
        Assert.Equal(ShelterRegistrationRequestStatus.Accepted, context.ShelterRegistrationRequests.Single(item => item.Id == request.Id).Status);
    }

    [Fact]
    public async Task AcceptRequestAsync_BlocksNonAdminUser()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);
        var request = await service.SubmitRequestAsync(CreateRequest());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcceptRequestAsync(request.Id, TestDbContextFactory.AdopterId));

        Assert.Contains("Only admins", exception.Message);
    }

    [Fact]
    public async Task RejectRequestAsync_DoesNotCreateUserOrShelter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var userManager = TestDbContextFactory.CreateUserManager(context);
        var service = CreateService(context, userManager: userManager);
        var request = await service.SubmitRequestAsync(CreateRequest());

        await service.RejectRequestAsync(request.Id, TestDbContextFactory.AdminId);

        Assert.Null(await userManager.FindByEmailAsync("new-shelter@example.test"));
        Assert.DoesNotContain(context.Shelters, shelter => shelter.Email == "new-shelter@example.test");
        Assert.Equal(ShelterRegistrationRequestStatus.Rejected, context.ShelterRegistrationRequests.Single(item => item.Id == request.Id).Status);
    }

    private static ShelterRegistrationRequestService CreateService(
        PawConnect.Data.ApplicationDbContext context,
        TestEmailService? emailService = null,
        Microsoft.AspNetCore.Identity.UserManager<PawConnect.Data.ApplicationUser>? userManager = null)
    {
        return new ShelterRegistrationRequestService(
            context,
            userManager ?? TestDbContextFactory.CreateUserManager(context),
            emailService ?? new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<ShelterRegistrationRequestService>.Instance);
    }

    private static ShelterRegistrationRequest CreateRequest(
        double? latitude = 46.75,
        double? longitude = 23.6,
        string email = "new-shelter@example.test")
    {
        return new ShelterRegistrationRequest
        {
            ShelterName = "New Shelter",
            ContactPersonName = "Shelter Contact",
            Email = email,
            PhoneNumber = "+40 700 000 005",
            City = "Cluj-Napoca",
            Neighborhood = "Buna Ziua",
            Address = "Strada Test 10",
            Description = "A demo shelter application for testing.",
            Website = "https://example.test",
            OpeningHours = "Mon-Fri 09:00-17:00",
            ReasonForJoining = "We want to manage adoptions through PawConnect.",
            Latitude = latitude,
            Longitude = longitude
        };
    }
}
