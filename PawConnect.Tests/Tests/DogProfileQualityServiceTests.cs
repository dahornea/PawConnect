using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class DogProfileQualityServiceTests
{
    [Fact]
    public async Task CheckFormAsync_MissingDescriptionCreatesIssue()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var result = await service.CheckFormAsync(BuildRequest(description: null, behavior: "She is calm with routine and takes treats gently."));

        Assert.Contains(result.Issues, issue =>
            issue.Category == DogProfileQualityCategory.MissingBehaviorInfo &&
            issue.FieldName == nameof(DogProfileQualityRequest.Description));
    }

    [Fact]
    public async Task CheckFormAsync_OverconfidentPhraseCreatesHighWarning()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var result = await service.CheckFormAsync(BuildRequest(
            description: "Bella is perfect for everyone and guaranteed to settle in any home.",
            behavior: "She enjoys a calm routine and short walks."));

        Assert.Contains(result.Issues, issue =>
            issue.Category == DogProfileQualityCategory.OverconfidentClaim &&
            issue.Severity == DogProfileQualitySeverity.High);
    }

    [Fact]
    public async Task CheckFormAsync_CompleteProfileScoresHigherThanSparseProfile()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        var sparse = await service.CheckFormAsync(BuildRequest(description: "Friendly dog.", behavior: null));
        var complete = await service.CheckFormAsync(BuildRequest(
            description: "Bella enjoys short walks, quiet indoor rest, and calm routines. She likes staying near people in the evening and settles after predictable activity.",
            behavior: "She takes treats gently, handles new situations with guidance, and has been observed walking politely near calm dogs.",
            medical: "Vaccinated and monitored during routine shelter checks.",
            catCompatibility: CatCompatibility.SlowIntroductions,
            dogCompatibility: DogCompatibility.SlowIntroductions,
            activityLevel: DogActivityLevel.Low,
            apartmentSuitability: ApartmentSuitability.Suitable));

        Assert.True(complete.OverallScore > sparse.OverallScore);
        Assert.Contains(complete.Strengths, strength => strength.Contains("Behavior", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckFormAsync_OpenAiFailureReturnsDeterministicFallback()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context, new FakeOpenAiDogProfileQualityClient
        {
            Response = OpenAiDogProfileQualityResponse.Failed("disabled")
        });

        var result = await service.CheckFormAsync(BuildRequest(description: null, behavior: null));

        Assert.False(result.UsedAi);
        Assert.Contains(result.Issues, issue => issue.Severity == DogProfileQualitySeverity.High);
    }

    [Fact]
    public async Task CheckFormAsync_UnsupportedAiRewriteClaimIsNotApplied()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var aiResult = new DogProfileQualityResult(
            92,
            "AI review completed.",
            [],
            ["The profile has useful details."],
            [],
            new DogProfileRewriteSuggestion(
                "Safer profile",
                "Bella is good with cats and safe with children.",
                "Bella is calm indoors."),
            ["Has Bella met cats or children?"],
            [],
            UsedAi: true,
            FallbackReason: null);
        var service = CreateService(context, new FakeOpenAiDogProfileQualityClient
        {
            Response = OpenAiDogProfileQualityResponse.Successful(aiResult)
        });

        var result = await service.CheckFormAsync(BuildRequest(
            description: "Bella enjoys short walks and settles indoors after a calm routine.",
            behavior: "She takes treats gently and responds well to routine."));

        Assert.True(result.UsedAi);
        Assert.Null(result.SuggestedRewrite?.Description);
        Assert.Contains(result.SafetyNotes, note => note.Contains("unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckDogAsync_ShelterCannotCheckAnotherShelterDog()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Other Shelter Dog", shelterId: TestDbContextFactory.OtherShelterId);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CheckDogAsync(dog.Id, TestDbContextFactory.ShelterId));

        Assert.Equal("Dog was not found for your shelter.", exception.Message);
    }

    private static DogProfileQualityService CreateService(
        DbContext context,
        IOpenAiDogProfileQualityClient? openAiClient = null)
    {
        return new DogProfileQualityService(
            (PawConnect.Data.ApplicationDbContext)context,
            openAiClient ?? new FakeOpenAiDogProfileQualityClient(),
            NullLogger<DogProfileQualityService>.Instance);
    }

    private static DogProfileQualityRequest BuildRequest(
        string? description,
        string? behavior,
        string? medical = null,
        CatCompatibility catCompatibility = CatCompatibility.Unknown,
        DogCompatibility dogCompatibility = DogCompatibility.Unknown,
        DogActivityLevel activityLevel = DogActivityLevel.Unknown,
        ApartmentSuitability apartmentSuitability = ApartmentSuitability.Unknown)
    {
        return new DogProfileQualityRequest
        {
            ShelterId = TestDbContextFactory.ShelterId,
            Name = "Bella",
            AgeYears = 3,
            AgeMonths = 0,
            Size = DogSize.Medium,
            Status = DogStatus.Available,
            BreedDisplay = "Labrador Retriever",
            CoatColor = "Golden",
            Description = description,
            BehaviorDescription = behavior,
            MedicalStatus = medical,
            CatCompatibility = catCompatibility,
            DogCompatibility = dogCompatibility,
            ChildrenCompatibility = ChildrenCompatibility.Unknown,
            ActivityLevel = activityLevel,
            ExperienceNeeded = DogExperienceNeeded.Unknown,
            ApartmentSuitability = apartmentSuitability
        };
    }

    private sealed class FakeOpenAiDogProfileQualityClient : IOpenAiDogProfileQualityClient
    {
        public OpenAiDogProfileQualityResponse Response { get; init; } =
            OpenAiDogProfileQualityResponse.Failed("OpenAI disabled for test.");

        public Task<OpenAiDogProfileQualityResponse> CheckAsync(
            DogProfileQualityRequest request,
            DogProfileQualityResult deterministicResult,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Response);
        }
    }
}
