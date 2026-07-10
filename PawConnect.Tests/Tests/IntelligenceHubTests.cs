using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Services.Intelligence;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class IntelligenceHubTests
{
    [Fact]
    public async Task DogProvider_CreatesExplainableSignalForIncompleteProfile()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var dog = TestDbContextFactory.CreateDog("Incomplete profile");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        var provider = new DogOperationsSignalProvider(
            TestDbContextFactory.CreateContextFactory(databaseName),
            new DogProfileCompletenessService(context),
            Options.Create(new IntelligenceHubOptions()));

        var signals = await provider.CollectSignalsAsync(
            new IntelligenceContext(IntelligenceAudienceType.Shelter, null, TestDbContextFactory.ShelterId, DateTime.UtcNow),
            CancellationToken.None);

        var signal = Assert.Single(signals, item => item.Key == $"DogProfileIncomplete:{dog.Id}");
        Assert.Contains(signal.Evidence, evidence => evidence.StartsWith("Completeness:", StringComparison.Ordinal));
        Assert.Contains(signal.RecommendedActions, action => action.Route == $"/shelter/dogs/edit/{dog.Id}");
    }

    [Fact]
    public async Task AdoptionProvider_CreatesDelaySignalOnlyAfterThreshold()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var dog = TestDbContextFactory.CreateDog("Waiting dog");
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        context.AdoptionRequests.Add(new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            Status = AdoptionRequestStatus.Pending,
            ReasonForAdoption = "Ready to adopt.",
            CreatedAt = DateTime.UtcNow.AddHours(-72)
        });
        await context.SaveChangesAsync();
        var provider = new AdoptionReviewSignalProvider(
            TestDbContextFactory.CreateContextFactory(databaseName),
            Options.Create(new IntelligenceHubOptions { ApplicationReviewWarningHours = 48 }));

        var signals = await provider.CollectSignalsAsync(
            new IntelligenceContext(IntelligenceAudienceType.Shelter, null, TestDbContextFactory.ShelterId, DateTime.UtcNow),
            CancellationToken.None);

        Assert.Contains(signals, signal => signal.Key.StartsWith("ApplicationReviewDelay:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VolunteerProvider_CreatesOverdueTaskSignal()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        context.VolunteerTasks.Add(new VolunteerTask
        {
            ShelterId = TestDbContextFactory.ShelterId,
            CreatedByUserId = TestDbContextFactory.ShelterUserId,
            Title = "Prepare medication",
            Status = VolunteerTaskStatus.Open,
            Priority = VolunteerTaskPriority.High,
            ScheduledStartUtc = DateTime.UtcNow.AddDays(-2),
            ScheduledEndUtc = DateTime.UtcNow.AddDays(-1),
            DueAtUtc = DateTime.UtcNow.AddHours(-20)
        });
        await context.SaveChangesAsync();
        var provider = new VolunteerTaskSignalProvider(
            TestDbContextFactory.CreateContextFactory(databaseName),
            Options.Create(new IntelligenceHubOptions { VolunteerTaskOverdueWarningHours = 12 }));

        var signals = await provider.CollectSignalsAsync(
            new IntelligenceContext(IntelligenceAudienceType.Shelter, null, TestDbContextFactory.ShelterId, DateTime.UtcNow),
            CancellationToken.None);

        Assert.Contains(signals, signal => signal.Key.StartsWith("VolunteerTaskOverdue:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StandardRule_UsesVisibleFactorsForDeterministicScore()
    {
        var signal = BuildSignal("Scoring:1", 34, 28, 16);
        var evaluation = new IntelligenceEvaluationContext(
            new IntelligenceContext(IntelligenceAudienceType.Admin, null, null, DateTime.UtcNow),
            [signal]);
        var rule = new StandardIntelligenceRule();

        var first = Assert.Single(await rule.EvaluateAsync(evaluation, CancellationToken.None));
        var second = Assert.Single(await rule.EvaluateAsync(evaluation, CancellationToken.None));

        Assert.Equal(78, first.PriorityScore);
        Assert.Equal(IntelligenceSeverity.High, first.Severity);
        Assert.Equal(first.PriorityScore, second.PriorityScore);
        Assert.Contains("Trigger:", first.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Engine_RepeatedEvaluationIsIdempotentAndMissingConditionAutoResolves()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        var factory = TestDbContextFactory.CreateContextFactory(databaseName);
        await using (var setup = TestDbContextFactory.CreateContext(databaseName)) { }
        var provider = new MutableSignalProvider { Signals = [BuildSignal("Lifecycle:1", 36, 26, 18)] };
        var engine = BuildEngine(factory, provider);

        await engine.EvaluateForShelterAsync(TestDbContextFactory.ShelterId);
        await engine.EvaluateForShelterAsync(TestDbContextFactory.ShelterId);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(1, await db.OperationalInsights.CountAsync());
            Assert.Equal(IntelligenceInsightStatus.Active, (await db.OperationalInsights.SingleAsync()).Status);
        }

        provider.Signals = [];
        await engine.EvaluateForShelterAsync(TestDbContextFactory.ShelterId);
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Equal(IntelligenceInsightStatus.Resolved, (await verification.OperationalInsights.SingleAsync()).Status);
    }

    [Fact]
    public async Task InsightService_EnforcesShelterScopeAndAcknowledgesOnlyOwnedInsight()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        var factory = TestDbContextFactory.CreateContextFactory(databaseName);
        await using (var context = TestDbContextFactory.CreateContext(databaseName))
        {
            context.OperationalInsights.AddRange(
                CreatePersistedInsight("shelter-one", TestDbContextFactory.ShelterId),
                CreatePersistedInsight("shelter-two", TestDbContextFactory.OtherShelterId));
            await context.SaveChangesAsync();
        }
        var service = new IntelligenceInsightService(
            factory,
            new StubEngine(),
            new IntelligenceRecommendationService(factory),
            new TestAuditLogService(),
            Options.Create(new IntelligenceHubOptions()));
        var scope = new IntelligenceScope(IntelligenceAudienceType.Shelter, ShelterId: TestDbContextFactory.ShelterId);

        var result = await service.GetInsightsAsync(scope, new IntelligenceInsightQuery());
        var owned = Assert.Single(result.Items);
        await service.AcknowledgeAsync(owned.Id, scope, TestDbContextFactory.ShelterUserId);

        await using var verification = await factory.CreateDbContextAsync();
        Assert.Equal(IntelligenceInsightStatus.Acknowledged, (await verification.OperationalInsights.SingleAsync(item => item.Fingerprint == "shelter-one")).Status);
        Assert.Equal(IntelligenceInsightStatus.Active, (await verification.OperationalInsights.SingleAsync(item => item.Fingerprint == "shelter-two")).Status);
    }

    [Fact]
    public async Task Engine_ProviderFailureDoesNotResolveExistingInsightFromThatProvider()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        var factory = TestDbContextFactory.CreateContextFactory(databaseName);
        await using (var context = TestDbContextFactory.CreateContext(databaseName))
        {
            var insight = CreatePersistedInsight("provider-failure", TestDbContextFactory.ShelterId);
            insight.SourceModule = "Tests";
            context.OperationalInsights.Add(insight);
            await context.SaveChangesAsync();
        }
        var engine = BuildEngine(factory, new ThrowingSignalProvider());

        var result = await engine.EvaluateForShelterAsync(TestDbContextFactory.ShelterId);

        await using var verification = await factory.CreateDbContextAsync();
        Assert.Equal(1, result.ProviderFailures);
        Assert.Equal(IntelligenceInsightStatus.Active, (await verification.OperationalInsights.SingleAsync()).Status);
    }

    [Fact]
    public async Task AdopterProvider_ReturnsOnlyCurrentUsersNewSavedSearchMatches()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var dog = TestDbContextFactory.CreateDog("Matching dog");
        var search = new SavedDogSearch { AdopterUserId = TestDbContextFactory.AdopterId, Name = "Calm matches", AlertsEnabled = true };
        context.Dogs.Add(dog);
        context.SavedDogSearches.Add(search);
        await context.SaveChangesAsync();
        context.SavedSearchMatches.Add(new SavedSearchMatch { SavedDogSearchId = search.Id, DogId = dog.Id, Status = SavedSearchMatchStatus.New, MatchScore = 82 });
        await context.SaveChangesAsync();
        var provider = new AdopterNextStepSignalProvider(TestDbContextFactory.CreateContextFactory(databaseName));

        var ownSignals = await provider.CollectSignalsAsync(new IntelligenceContext(IntelligenceAudienceType.Adopter, TestDbContextFactory.AdopterId, null, DateTime.UtcNow), CancellationToken.None);
        var otherSignals = await provider.CollectSignalsAsync(new IntelligenceContext(IntelligenceAudienceType.Adopter, TestDbContextFactory.SecondAdopterId, null, DateTime.UtcNow), CancellationToken.None);

        Assert.Contains(ownSignals, signal => signal.Key == $"SavedSearchNewMatches:{search.Id}");
        Assert.Empty(otherSignals);
    }

    private static IntelligenceSignal BuildSignal(string key, params int[] points)
        => new(
            key, IntelligenceCategory.Workload, "Tests", "TestEntity", "1", "Test entity", null, TestDbContextFactory.ShelterId,
            "Test operational insight", "A factual test condition exists.", "It affects a real workflow.", "the test condition disappears",
            "Configured test threshold crossed", ["Evidence A", "Evidence B"],
            points.Select((point, index) => new IntelligenceScoreFactor($"Factor {index + 1}", point, $"Adds {point} points.")).ToList(),
            [new RecommendedActionDto("open", "Open workflow", "Open the related workflow.", "Navigate", "/shelter/dogs", "Shelter", IsPrimary: true)],
            DateTime.UtcNow);

    private static IntelligenceEngine BuildEngine(IDbContextFactory<ApplicationDbContext> factory, IIntelligenceSignalProvider provider)
        => new(
            [provider],
            [new StandardIntelligenceRule()],
            factory,
            new TestNotificationService(),
            new TestAuditLogService(),
            Options.Create(new IntelligenceHubOptions()),
            NullLogger<IntelligenceEngine>.Instance);

    private static OperationalInsight CreatePersistedInsight(string fingerprint, int shelterId)
        => new()
        {
            Fingerprint = fingerprint,
            AudienceType = IntelligenceAudienceType.Shelter,
            ShelterId = shelterId,
            Category = IntelligenceCategory.Workload,
            InsightType = "Test",
            SourceModule = "Tests",
            EntityType = "TestEntity",
            Title = fingerprint,
            Summary = "Test summary",
            Severity = IntelligenceSeverity.Medium,
            PriorityScore = 55,
            ConfidenceLabel = "High",
            Explanation = "Test explanation",
            EvidenceJson = "[]",
            ScoreBreakdownJson = "[]",
            RecommendedActionsJson = "[]"
        };

    private sealed class MutableSignalProvider : IIntelligenceSignalProvider
    {
        public string ProviderKey => "Tests";
        public IReadOnlyCollection<IntelligenceSignal> Signals { get; set; } = [];
        public Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(IntelligenceContext context, CancellationToken cancellationToken) => Task.FromResult(Signals);
    }

    private sealed class ThrowingSignalProvider : IIntelligenceSignalProvider
    {
        public string ProviderKey => "Tests";
        public Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(IntelligenceContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Provider unavailable.");
    }

    private sealed class StubEngine : IIntelligenceEngine
    {
        public Task<IntelligenceEvaluationResult> EvaluateForShelterAsync(int shelterId, CancellationToken cancellationToken = default) => Task.FromResult(Result(IntelligenceAudienceType.Shelter, shelterId: shelterId));
        public Task<IntelligenceEvaluationResult> EvaluateForAdminAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result(IntelligenceAudienceType.Admin));
        public Task<IntelligenceEvaluationResult> EvaluateForAdopterAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(Result(IntelligenceAudienceType.Adopter, userId));
        public Task RefreshActiveInsightsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        private static IntelligenceEvaluationResult Result(IntelligenceAudienceType type, string? userId = null, int? shelterId = null) => new(type, userId, shelterId, 0, 0, 0, 0, 0, 0, DateTime.UtcNow, TimeSpan.Zero);
    }

    private sealed class TestAuditLogService : IAuditLogService
    {
        public Task LogAsync(AuditLog log) => Task.CompletedTask;
        public Task LogAsync(string action, string entityName, string? entityId, string description, string? userId = null, string? userEmail = null, string? userRole = null, string? additionalData = null) => Task.CompletedTask;
        public Task LogSystemAsync(string action, string entityName, string? entityId, string description, string? additionalData = null) => Task.CompletedTask;
        public Task LogUserActionAsync(string action, string entityType, string? entityId, string summary, object? details = null, string severity = "Information", string eventType = "Business") => Task.CompletedTask;
        public Task LogSystemEventAsync(string action, string entityType, string? entityId, string summary, object? details = null, string severity = "Information") => Task.CompletedTask;
        public Task LogCopilotEventAsync(string action, string? entityId, string summary, object? details = null, string severity = "Information") => Task.CompletedTask;
        public Task<List<AuditLog>> GetRecentLogsAsync(int count) => Task.FromResult(new List<AuditLog>());
        public Task<List<AuditLog>> GetLogsAsync(string? action = null, string? entityName = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null, string? severity = null, string? eventType = null, string? correlationId = null, int take = 200) => Task.FromResult(new List<AuditLog>());
        public Task<List<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId) => Task.FromResult(new List<AuditLog>());
    }

    private sealed class TestNotificationService : INotificationService
    {
        public Task CreateNotificationAsync(string userId, string title, string message, NotificationCategory category, NotificationType type, string? link = null, string? relatedEntityName = null, string? relatedEntityId = null, TimeSpan? suppressDuplicatesWithin = null) => Task.CompletedTask;
        public Task<List<Notification>> GetNotificationsForUserAsync(string userId, int count = 20) => Task.FromResult(new List<Notification>());
        public Task<List<Notification>> GetNotificationsForUserAsync(string userId, NotificationCategory? category, bool unreadOnly, int count = 100) => Task.FromResult(new List<Notification>());
        public Task<int> GetUnreadCountAsync(string userId) => Task.FromResult(0);
        public Task MarkAsReadAsync(int notificationId, string userId) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(string userId) => Task.CompletedTask;
        public Task DeleteNotificationAsync(int notificationId, string userId) => Task.CompletedTask;
    }
}
