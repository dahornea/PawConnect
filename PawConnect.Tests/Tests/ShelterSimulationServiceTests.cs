using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Services.Intelligence;
using PawConnect.Services.Simulation;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class ShelterSimulationServiceTests
{
    [Fact]
    public async Task Engine_SameBaselineAndAssumptions_ReturnsSameProjection()
    {
        var engine = CreateEngine();
        var baseline = Baseline();
        var request = new SimulationRunRequestDto(1, SimulationScopeType.Shelter, 14, [new(SimulationAssumptionType.DogIntake, 4, 2)]);

        var first = await engine.RunAsync(baseline, request, false);
        var second = await engine.RunAsync(baseline, request, false);

        Assert.Equal(first.ProjectedState, second.ProjectedState);
        Assert.Equal(first.ProjectedWorkloadScore, second.ProjectedWorkloadScore);
        Assert.Equal(first.RiskChanges.Select(item => (item.Key, item.Impact)), second.RiskChanges.Select(item => (item.Key, item.Impact)));
    }

    [Fact]
    public async Task Run_DogIntake_DoesNotCreateOrModifyRealDogs()
    {
        var (service, databaseName) = CreateService();
        await using (var db = TestDbContextFactory.CreateContextFactory(databaseName).CreateDbContext())
        {
            db.Dogs.Add(TestDbContextFactory.CreateDog("Baseline dog"));
            await db.SaveChangesAsync();
        }

        var request = new SimulationRunRequestDto(TestDbContextFactory.ShelterId, SimulationScopeType.Shelter, 7, [new(SimulationAssumptionType.DogIntake, 6, 1)]);
        var result = await service.RunAsync(request, ShelterAccess());

        await using var verification = TestDbContextFactory.CreateContextFactory(databaseName).CreateDbContext();
        Assert.Equal(7, result.ProjectedState.CurrentDogs);
        Assert.Single(verification.Dogs);
        Assert.Equal("Baseline dog", verification.Dogs.Single().Name);
    }

    [Fact]
    public async Task Engine_VolunteerShortage_IncreasesWorkload()
    {
        var engine = CreateEngine();
        var request = new SimulationRunRequestDto(1, SimulationScopeType.Shelter, 7, [new(SimulationAssumptionType.VolunteerUnavailable, 3, 1)]);

        var result = await engine.RunAsync(Baseline(activeVolunteers: 4), request, false);

        Assert.True(result.ProjectedWorkloadScore > result.BaselineWorkloadScore);
        Assert.Equal(1, result.ProjectedState.ActiveVolunteers);
    }

    [Fact]
    public async Task Run_ShelterUserRequestsOtherShelter_IsRejected()
    {
        var (service, _) = CreateService();
        var request = new SimulationRunRequestDto(TestDbContextFactory.OtherShelterId, SimulationScopeType.Shelter, 7, [new(SimulationAssumptionType.DogIntake, 1, 1)]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(request, ShelterAccess()));
    }

    [Fact]
    public async Task SaveAndRun_PersistsScenarioAndSnapshotRunOnly()
    {
        var (service, databaseName) = CreateService();
        await using (var db = TestDbContextFactory.CreateContextFactory(databaseName).CreateDbContext())
        {
            db.Dogs.Add(TestDbContextFactory.CreateDog("Stored dog"));
            await db.SaveChangesAsync();
        }

        var request = new SimulationSaveRequestDto(null, "Weekend intake plan", "Capacity check", new SimulationRunRequestDto(TestDbContextFactory.ShelterId, SimulationScopeType.Shelter, 7, [new(SimulationAssumptionType.DogIntake, 3, 1)]));
        var saved = await service.SaveAndRunAsync(request, ShelterAccess());

        await using var verification = TestDbContextFactory.CreateContextFactory(databaseName).CreateDbContext();
        Assert.Equal(1, await verification.ShelterSimulationScenarios.CountAsync());
        Assert.Equal(1, await verification.ShelterSimulationRuns.CountAsync());
        Assert.Single(verification.Dogs);
        Assert.StartsWith("{", (await verification.ShelterSimulationRuns.SingleAsync()).BaselineSnapshotJson);
        Assert.True(saved.RunId > 0);
    }

    private static SimulationAccessContext ShelterAccess() => new(TestDbContextFactory.ShelterUserId, false, TestDbContextFactory.ShelterId);

    private static ISimulationEngine CreateEngine()
    {
        ISimulationAssumptionApplier[] appliers = [new DogOperationsAssumptionApplier(), new VolunteerCapacityAssumptionApplier(), new AdoptionWorkflowAssumptionApplier(), new ProfileQualityAssumptionApplier(), new NotificationReliabilityAssumptionApplier(), new ShelterCapacityAssumptionApplier()];
        return new SimulationEngine(appliers, new SimulationValidationService(), new SimulationImpactAnalyzer(new StandardIntelligenceRule()), new SimulationRecommendationService());
    }

    private static (IShelterSimulationService Service, string DatabaseName) CreateService()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        using (TestDbContextFactory.CreateContext(databaseName)) { }
        var service = new ShelterSimulationService(TestDbContextFactory.CreateContextFactory(databaseName), CreateEngine(), new SimulationValidationService(), new TestAuditLogService(), NullLogger<ShelterSimulationService>.Instance);
        return (service, databaseName);
    }

    private static SimulationBaselineDto Baseline(int activeVolunteers = 4) => new(1, "Test Shelter", SimulationScopeType.Shelter, 12, 2, 7, 1, activeVolunteers, 5, 1, 4, 2, 1, 0, 0, DateTime.UnixEpoch);

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
}
