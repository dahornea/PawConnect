using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class CopilotHistoryFeedbackServiceTests
{
    [Fact]
    public async Task AdoptionCopilot_SavesSessionForCurrentAdopter()
    {
        await using var context = TestDbContextFactory.CreateContext();
        SeedProfile(context);
        var dog = TestDbContextFactory.CreateDog("Calm Match");
        dog.Description = "Calm medium dog who settles indoors after short walks.";
        dog.BehaviorDescription = "Gentle routine and easy to redirect.";
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var historyService = new CopilotHistoryService(context);
        var service = CreateCopilotService(context, historyService);

        var response = await service.AskAsync(TestDbContextFactory.AdopterId, "calm medium dog for an apartment");

        Assert.NotNull(response.SessionId);
        Assert.Single(context.CopilotSessions);
        var session = context.CopilotSessions.Single();
        Assert.Equal(TestDbContextFactory.AdopterId, session.AdopterUserId);
        Assert.Contains(dog.Id.ToString(), session.ResultDogIdsJson);
        Assert.Contains("Size", session.AppliedConstraintsJson);
    }

    [Fact]
    public async Task History_ListShowsOnlyCurrentAdopterSessions()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var historyService = new CopilotHistoryService(context);
        await historyService.SaveSessionAsync(
            TestDbContextFactory.AdopterId,
            "small dog",
            BuildResponse(1));
        await historyService.SaveSessionAsync(
            TestDbContextFactory.SecondAdopterId,
            "large dog",
            BuildResponse(2));

        var sessions = await historyService.GetRecentSessionsAsync(TestDbContextFactory.AdopterId);

        var session = Assert.Single(sessions);
        Assert.Equal("small dog", session.QuerySummary);
    }

    [Fact]
    public async Task Feedback_CanBeSubmittedOnlyForOwnedSessionResult()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Feedback Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var historyService = new CopilotHistoryService(context);
        var sessionId = await historyService.SaveSessionAsync(
            TestDbContextFactory.AdopterId,
            "friendly dog",
            BuildResponse(dog.Id, dog));
        var feedbackService = new CopilotFeedbackService(context);

        var feedback = await feedbackService.SubmitFeedbackAsync(
            new SubmitCopilotFeedbackRequest(sessionId, dog.Id, CopilotFeedbackType.Positive),
            TestDbContextFactory.AdopterId);

        Assert.Equal(CopilotFeedbackType.Positive, feedback.FeedbackType);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            feedbackService.SubmitFeedbackAsync(
                new SubmitCopilotFeedbackRequest(sessionId, dog.Id, CopilotFeedbackType.Negative),
                TestDbContextFactory.SecondAdopterId));
    }

    [Fact]
    public async Task Feedback_DuplicateSubmissionUpdatesExistingRow()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Update Feedback Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var historyService = new CopilotHistoryService(context);
        var sessionId = await historyService.SaveSessionAsync(
            TestDbContextFactory.AdopterId,
            "friendly dog",
            BuildResponse(dog.Id, dog));
        var feedbackService = new CopilotFeedbackService(context);

        await feedbackService.SubmitFeedbackAsync(
            new SubmitCopilotFeedbackRequest(sessionId, dog.Id, CopilotFeedbackType.Positive),
            TestDbContextFactory.AdopterId);
        var updated = await feedbackService.SubmitFeedbackAsync(
            new SubmitCopilotFeedbackRequest(sessionId, dog.Id, CopilotFeedbackType.NotRelevant, "Wrong fit"),
            TestDbContextFactory.AdopterId);

        Assert.Single(context.CopilotResultFeedbacks);
        Assert.Equal(CopilotFeedbackType.NotRelevant, updated.FeedbackType);
        Assert.Equal("Wrong fit", updated.OptionalComment);
    }

    [Fact]
    public async Task Feedback_RejectsDogThatWasNotReturnedInSession()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var returnedDog = TestDbContextFactory.CreateDog("Returned Dog");
        var otherDog = TestDbContextFactory.CreateDog("Other Dog");
        context.Dogs.AddRange(returnedDog, otherDog);
        await context.SaveChangesAsync();
        var historyService = new CopilotHistoryService(context);
        var sessionId = await historyService.SaveSessionAsync(
            TestDbContextFactory.AdopterId,
            "friendly dog",
            BuildResponse(returnedDog.Id, returnedDog));
        var feedbackService = new CopilotFeedbackService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            feedbackService.SubmitFeedbackAsync(
                new SubmitCopilotFeedbackRequest(sessionId, otherDog.Id, CopilotFeedbackType.NotRelevant),
                TestDbContextFactory.AdopterId));
    }

    [Fact]
    public async Task Explanation_UsesBackendEvidenceAndCautionTags()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var dog = TestDbContextFactory.CreateDog("Evidence Dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var historyService = new CopilotHistoryService(context);
        var response = BuildResponse(dog.Id, dog);
        var sessionId = await historyService.SaveSessionAsync(
            TestDbContextFactory.AdopterId,
            "apartment dog",
            response);
        var feedbackService = new CopilotFeedbackService(context);

        var explanation = await feedbackService.BuildExplanationAsync(
            sessionId,
            TestDbContextFactory.AdopterId,
            response.Results[0],
            response.AppliedConstraints ?? []);

        Assert.Contains(explanation.DirectEvidence, item => item.Contains("Settles quickly"));
        Assert.Contains(explanation.CautionEvidence, item => item.Contains("Ask shelter"));
        Assert.Contains("advisory", explanation.AdvisoryDisclaimer, StringComparison.OrdinalIgnoreCase);
    }

    private static AdoptionCopilotResponse BuildResponse(int dogId, Dog? dog = null)
    {
        dog ??= TestDbContextFactory.CreateDog($"Dog {dogId}");
        dog.Id = dogId;

        return new AdoptionCopilotResponse(
            "Found a match.",
            [
                new AdoptionCopilotDogResult(
                    dogId,
                    dog,
                    72,
                    "Good match",
                    ["Settles indoors after walks."],
                    "Ask the shelter for current details.",
                    MatchedCriteria:
                    [
                        new AdoptionCopilotConstraint("Home", "Apartment")
                    ],
                    DisplayTags: ["Settles quickly"],
                    CautionTags: ["Ask shelter about apartment fit"],
                    PositiveEvidence:
                    [
                        new EvidenceItem("Settles quickly", "Direct", "Description", "settles indoors")
                    ],
                    CautionEvidence:
                    [
                        new EvidenceItem("Ask shelter about apartment fit", "Caution", "Behavior", null)
                    ],
                    MissingEvidence:
                    [
                        new EvidenceItem("Apartment history", "Missing", "Profile", null)
                    ])
            ],
            false,
            false,
            false,
            AppliedConstraints:
            [
                new AdoptionCopilotConstraint("Home", "Apartment")
            ]);
    }

    private static AdoptionCopilotService CreateCopilotService(
        ApplicationDbContext context,
        ICopilotHistoryService historyService)
    {
        var toolService = new AdoptionCopilotToolService(
            context,
            new FakeSemanticDogSearchService(),
            new FakeGeocodingService(),
            new DistanceService());

        return new AdoptionCopilotService(
            context,
            toolService,
            new FakeOpenAiAdoptionCopilotClient(),
            Options.Create(new OpenAiSettings { Enabled = false }),
            NullLogger<AdoptionCopilotService>.Instance,
            historyService);
    }

    private static void SeedProfile(ApplicationDbContext context)
    {
        context.AdopterProfiles.Add(new AdopterProfile
        {
            ApplicationUserId = TestDbContextFactory.AdopterId,
            FullName = "Test Adopter",
            City = "Bucharest",
            HousingType = HousingType.Apartment,
            HasYard = false,
            HasChildren = false,
            HasOtherPets = false
        });
        context.SaveChanges();
    }

    private sealed class FakeOpenAiAdoptionCopilotClient : IOpenAiAdoptionCopilotClient
    {
        public Task<OpenAiAdoptionCopilotResponse> AskWithToolsAsync(
            AdoptionCopilotToolOpenAiRequest request,
            OpenAiCopilotToolExecutor executeToolAsync,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OpenAiAdoptionCopilotResponse.Failed("disabled"));
        }
    }

    private sealed class FakeSemanticDogSearchService : ISemanticDogSearchService
    {
        public Task<IReadOnlyList<SemanticDogSearchResult>> SearchDogsAsync(
            string query,
            string? adopterUserId,
            int count = 10,
            SemanticDogSearchOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SemanticDogSearchResult>>([]);
        }
    }

    private sealed class FakeGeocodingService : IGeocodingService
    {
        public Task<GeocodingResult?> FindCoordinatesAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GeocodingResult?>(null);
        }

        public Task<GeocodingResult?> FindCoordinatesAsync(string address, string city, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GeocodingResult?>(null);
        }

        public Task<IReadOnlyList<AddressSuggestion>> SearchAddressSuggestionsAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AddressSuggestion>>([]);
        }

        public Task<ReverseGeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ReverseGeocodingResult?>(null);
        }
    }
}
