using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests.Integration;

public class ServiceFlowIntegrationTests
{
    [Fact]
    public async Task PublicDogFlow_ReturnsOnlyPublicSafeDogsForListingAndFeaturedPreview()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.AddRange(
            TestDbContextFactory.CreateDog("Available Flow Dog", DogStatus.Available),
            TestDbContextFactory.CreateDog("Reserved Flow Dog", DogStatus.Reserved),
            TestDbContextFactory.CreateDog("Adopted Flow Dog", DogStatus.Adopted),
            TestDbContextFactory.CreateDog("Treatment Flow Dog", DogStatus.InTreatment));
        await context.SaveChangesAsync();
        var dogService = new DogService(context);

        var publicDogs = await dogService.GetAvailableDogsAsync();
        var featuredDogs = publicDogs.Take(4).ToList();

        Assert.All(publicDogs, dog => Assert.True(dog.Status is DogStatus.Available or DogStatus.Reserved));
        Assert.Contains(publicDogs, dog => dog.Name == "Available Flow Dog");
        Assert.Contains(publicDogs, dog => dog.Name == "Reserved Flow Dog");
        Assert.DoesNotContain(publicDogs, dog => dog.Name == "Adopted Flow Dog");
        Assert.DoesNotContain(publicDogs, dog => dog.Name == "Treatment Flow Dog");
        Assert.All(featuredDogs, dog => Assert.True(dog.Status is DogStatus.Available or DogStatus.Reserved));
    }

    [Fact]
    public async Task FavoritesAndDogDeletionFlow_KeepsFavoritesPrivateAndDeletesFavoriteOnlyDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Favorite Delete Flow Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var favoriteService = new FavoriteDogService(context, TestDbContextFactory.CreateUserManager(context));
        var dogService = new DogService(context);

        await favoriteService.AddFavoriteAsync(TestDbContextFactory.AdopterId, dog.Id);
        await favoriteService.AddFavoriteAsync(TestDbContextFactory.AdopterId, dog.Id);
        await favoriteService.AddFavoriteAsync(TestDbContextFactory.SecondAdopterId, dog.Id);

        var firstAdopterFavorites = await favoriteService.GetFavoritesForUserAsync(TestDbContextFactory.AdopterId);
        var secondAdopterFavorites = await favoriteService.GetFavoritesForUserAsync(TestDbContextFactory.SecondAdopterId);
        await dogService.DeleteDogAsync(dog.Id, TestDbContextFactory.ShelterId);

        Assert.Single(firstAdopterFavorites);
        Assert.Single(secondAdopterFavorites);
        Assert.False(await context.Dogs.AnyAsync(d => d.Id == dog.Id));
        Assert.False(await context.FavoriteDogs.AnyAsync(f => f.DogId == dog.Id));
    }

    [Fact]
    public async Task AdoptionRequestFlow_SubmitViewAcceptAndNotifyWithPdfAttachment()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Adoption Flow Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var emailService = new TestEmailService();
        var adoptionService = CreateAdoptionRequestService(context, emailService);

        await adoptionService.CreateRequestAsync(TestDbContextFactory.AdopterId, dog.Id, new AdoptionRequestQuestionnaire(
            "I have a safe home and time for daily walks.",
            3,
            "I live close to a park.",
            FutureVisit()));

        var request = await context.AdoptionRequests.SingleAsync();
        var owningShelterRequests = await adoptionService.GetRequestsForShelterAsync(TestDbContextFactory.ShelterId);
        var otherShelterRequests = await adoptionService.GetRequestsForShelterAsync(TestDbContextFactory.OtherShelterId);
        var duplicateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adoptionService.CreateRequestAsync(TestDbContextFactory.AdopterId, dog.Id, new AdoptionRequestQuestionnaire("Duplicate request", null, null)));

        await adoptionService.ConfirmVisitAsync(request.Id, TestDbContextFactory.ShelterId, TestDbContextFactory.ShelterUserId);
        var confirmedRequest = await context.AdoptionRequests.Include(r => r.Dog).SingleAsync(r => r.Id == request.Id);

        Assert.Equal("I have a safe home and time for daily walks.", request.ReasonForAdoption);
        Assert.Equal(3, request.HoursAlonePerDay);
        Assert.Equal("I live close to a park.", request.AdditionalInformation);
        Assert.Single(owningShelterRequests);
        Assert.Empty(otherShelterRequests);
        Assert.Equal("You already have a pending request for this dog.", duplicateException.Message);
        Assert.Equal(AdoptionRequestStatus.VisitConfirmed, confirmedRequest.Status);
        Assert.Equal(AdoptionVisitStatus.Confirmed, confirmedRequest.VisitStatus);
        Assert.Equal(DogStatus.Reserved, confirmedRequest.Dog!.Status);
        Assert.True(await context.DogStatusHistories.AnyAsync(h => h.DogId == dog.Id));
        Assert.Equal(2, emailService.SentEmails.Count);
        Assert.Contains(emailService.SentEmails, email => email.To == "shelter@test.com" && HasPdfAttachment(email.Attachments, "AdoptionRequestReport.pdf"));
        Assert.Contains(emailService.SentEmails, email => email.To == "adopter@test.com" && HasCalendarAttachment(email.Attachments));
        Assert.All(emailService.SentEmails, email =>
        {
            Assert.NotNull(email.HtmlBody);
            Assert.Contains("PawConnect", email.HtmlBody!);
        });
    }

    [Fact]
    public async Task AdoptionRequestFlow_RejectAndCancelRespectOwnershipAndPendingRules()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Reject Flow Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var adoptionService = CreateAdoptionRequestService(context, new TestEmailService());

        await adoptionService.CreateRequestAsync(TestDbContextFactory.AdopterId, dog.Id, new AdoptionRequestQuestionnaire("Ready to adopt.", null, null, FutureVisit()));
        var request = await context.AdoptionRequests.SingleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adoptionService.CancelRequestAsync(request.Id, TestDbContextFactory.SecondAdopterId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adoptionService.RejectRequestAsync(request.Id, TestDbContextFactory.OtherShelterId));
        await adoptionService.RejectRequestAsync(request.Id, TestDbContextFactory.ShelterId);

        Assert.Equal(AdoptionRequestStatus.Rejected, (await context.AdoptionRequests.FindAsync(request.Id))!.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adoptionService.CancelRequestAsync(request.Id, TestDbContextFactory.AdopterId));
    }

    [Fact]
    public async Task DogCreateImageAndAgeFlow_SavesImagesAndFormatsAge()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dogService = new DogService(context);
        var imageService = new DogImageService(context);
        var puppy = TestDbContextFactory.CreateDog("Puppy Flow Dog", ageYears: 0, ageMonths: 7);

        await dogService.CreateDogAsync(puppy, TestDbContextFactory.ShelterId);
        await imageService.AddDogImageAsync(puppy.Id, TestDbContextFactory.ShelterId, new DogImage
        {
            ImageUrl = "https://example.com/puppy-main.jpg",
            IsMainImage = true
        });
        await imageService.AddDogImageAsync(puppy.Id, TestDbContextFactory.ShelterId, new DogImage
        {
            ImageUrl = "https://example.com/puppy-gallery.jpg"
        });

        var savedDog = await dogService.GetDogForShelterAsync(puppy.Id, TestDbContextFactory.ShelterId);
        var images = await imageService.GetImagesForDogAsync(puppy.Id);

        Assert.Equal("7 months old", DogAgeFormatter.Format(savedDog!));
        Assert.Equal(2, images.Count);
        Assert.Single(images, image => image.IsMainImage);
    }

    [Fact]
    public async Task ResourceStockFlow_EnforcesOwnershipLowStockAndNotifications()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var emailService = new TestEmailService();
        var resourceService = CreateResourceStockService(context, emailService);

        await resourceService.CreateResourceAsync(new ResourceStock
        {
            Name = "Adult Food Flow",
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            FoodTypeId = TestDbContextFactory.AdultFoodTypeId,
            Quantity = 20,
            Unit = "kg",
            LowStockThreshold = 5
        }, TestDbContextFactory.ShelterId);
        var resource = await context.ResourceStocks.SingleAsync();

        resource.Quantity = 5;
        await resourceService.UpdateResourceAsync(resource, TestDbContextFactory.ShelterId);
        var lowStock = await resourceService.GetLowStockResourcesForShelterAsync(TestDbContextFactory.ShelterId);

        resource.ResourceCategoryId = TestDbContextFactory.MedicineCategoryId;
        resource.FoodTypeId = TestDbContextFactory.AdultFoodTypeId;
        resource.Quantity = 10;
        await resourceService.UpdateResourceAsync(resource, TestDbContextFactory.ShelterId);
        var nonLowStock = await resourceService.GetLowStockResourcesForShelterAsync(TestDbContextFactory.ShelterId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resourceService.DeleteResourceAsync(resource.Id, TestDbContextFactory.OtherShelterId));

        Assert.Contains(lowStock, item => item.Id == resource.Id);
        Assert.DoesNotContain(nonLowStock, item => item.Id == resource.Id);
        Assert.Null((await context.ResourceStocks.FindAsync(resource.Id))!.FoodTypeId);
        Assert.Contains(emailService.SentEmails, email => email.To == "shelter@test.com" && HasPdfAttachment(email.Attachments, "LowStockResourceReport.pdf"));
        Assert.Contains(emailService.SentEmails, email => email.HtmlBody?.Contains("Low stock warning") == true);
    }

    private static AdoptionRequestService CreateAdoptionRequestService(ApplicationDbContext context, TestEmailService emailService)
    {
        return new AdoptionRequestService(
            context,
            emailService,
            new TestPdfReportService(),
            NullLogger<AdoptionRequestService>.Instance,
            TestDbContextFactory.CreateUserManager(context));
    }

    private static ResourceStockService CreateResourceStockService(ApplicationDbContext context, TestEmailService emailService)
    {
        return new ResourceStockService(
            context,
            emailService,
            new TestPdfReportService(),
            NullLogger<ResourceStockService>.Instance);
    }

    private static bool HasPdfAttachment(List<EmailAttachment>? attachments, string fileName)
    {
        return attachments?.Any(attachment =>
            attachment.FileName == fileName &&
            attachment.ContentType == "application/pdf" &&
            attachment.Content.Length > 0) == true;
    }

    private static bool HasCalendarAttachment(List<EmailAttachment>? attachments)
    {
        return attachments?.Any(attachment =>
            attachment.ContentType == "text/calendar" &&
            attachment.FileName.EndsWith(".ics", StringComparison.OrdinalIgnoreCase) &&
            attachment.Content.Length > 0) == true;
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
