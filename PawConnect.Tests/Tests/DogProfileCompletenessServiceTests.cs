using Microsoft.EntityFrameworkCore;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class DogProfileCompletenessServiceTests
{
    [Fact]
    public async Task CalculateForShelterDogAsync_CompleteDogGetsExcellentScore()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = BuildCompleteDog("Polished Bella");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = new DogProfileCompletenessService(context);

        var result = await service.CalculateForShelterDogAsync(dog.Id, TestDbContextFactory.ShelterId);

        Assert.Equal("Excellent", result.Label);
        Assert.True(result.ScorePercent >= 85);
        Assert.Empty(result.MissingItems);
    }

    [Fact]
    public async Task CalculateForShelterDogAsync_IncompleteDogShowsMissingCriticalItems()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Sparse Dog");
        dog.Description = null;
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = new DogProfileCompletenessService(context);

        var result = await service.CalculateForShelterDogAsync(dog.Id, TestDbContextFactory.ShelterId);

        Assert.NotEqual("Excellent", result.Label);
        Assert.Contains(result.MissingItems, item => item.Label == "Public description" && item.IsCritical);
        Assert.Contains(result.Recommendations, item => item.Title == "Improve the public description");
    }

    [Fact]
    public async Task CalculateForShelterDogAsync_ShelterCannotScoreAnotherShelterDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = BuildCompleteDog("Other Shelter Dog", TestDbContextFactory.OtherShelterId);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = new DogProfileCompletenessService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateForShelterDogAsync(dog.Id, TestDbContextFactory.ShelterId));

        Assert.Equal("Dog was not found for your shelter.", exception.Message);
    }

    [Fact]
    public async Task GetAdminCompletenessStatsAsync_SummarizesAllDogs()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Dogs.Add(BuildCompleteDog("Complete Dog"));
        var incompleteDog = TestDbContextFactory.CreateDog("Incomplete Dog", shelterId: TestDbContextFactory.OtherShelterId);
        incompleteDog.Description = null;
        context.Dogs.Add(incompleteDog);
        await context.SaveChangesAsync();
        var service = new DogProfileCompletenessService(context);

        var summary = await service.GetAdminCompletenessStatsAsync();

        Assert.Equal(2, summary.TotalDogs);
        Assert.Equal(1, summary.ExcellentCount);
        Assert.Contains(summary.DogsNeedingAttention, dog => dog.DogName == "Incomplete Dog");
    }

    private static Dog BuildCompleteDog(string name, int shelterId = TestDbContextFactory.ShelterId)
    {
        var dog = TestDbContextFactory.CreateDog(name, shelterId: shelterId);
        dog.Breed = "Labrador Retriever";
        dog.CoatColor = "Golden";
        dog.Description = $"{name} enjoys daily walks, calm routines, and settling indoors after outdoor time. Shelter staff have observed steady manners around familiar people and a predictable evening routine.";
        dog.BehaviorDescription = "Takes treats gently, responds well to routine, and can be redirected with praise during new situations.";
        dog.MedicalStatus = "Vaccinated, dewormed, and monitored during routine shelter checks.";
        dog.CatCompatibility = CatCompatibility.SlowIntroductions;
        dog.DogCompatibility = DogCompatibility.SlowIntroductions;
        dog.ChildrenCompatibility = ChildrenCompatibility.OlderChildrenOnly;
        dog.ActivityLevel = DogActivityLevel.Medium;
        dog.ExperienceNeeded = DogExperienceNeeded.Beginner;
        dog.ApartmentSuitability = ApartmentSuitability.MaybeWithRoutine;
        dog.CompatibilityNotes = "Best with calm introductions, a predictable home routine, and adopters who can continue gentle training.";
        dog.PreferredFoodTypeId = TestDbContextFactory.AdultFoodTypeId;
        dog.DailyFoodAmountGrams = 320;
        dog.Images.Add(new DogImage { ImageUrl = "https://example.com/dog-main.jpg", IsMainImage = true });
        dog.Images.Add(new DogImage { ImageUrl = "https://example.com/dog-gallery.jpg" });
        dog.MedicalRecords.Add(new MedicalRecord
        {
            VaccineName = "Annual vaccination",
            TreatmentDescription = "Routine check",
            RecordDate = DateTime.UtcNow.AddDays(-10),
            Notes = "No complications reported."
        });

        return dog;
    }
}
