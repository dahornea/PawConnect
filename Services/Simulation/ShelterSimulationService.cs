using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Simulation;

public sealed class ShelterSimulationService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    ISimulationEngine engine,
    ISimulationValidationService validation,
    IAuditLogService auditLogService,
    ILogger<ShelterSimulationService> logger) : IShelterSimulationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<SimulationTemplateDto> Templates =
    [
        new("intake-surge", "Sudden intake surge", "Project five additional dogs entering care and review shelter capacity.", 14, [new(SimulationAssumptionType.DogIntake, 5, 1)]),
        new("volunteer-shortage", "Volunteer shortage", "Project three active volunteers becoming unavailable.", 14, [new(SimulationAssumptionType.VolunteerUnavailable, 3, 1)]),
        new("incoming-transfer", "Urgent incoming transfer", "Project an incoming dog transfer and its operational effect.", 7, [new(SimulationAssumptionType.IncomingTransfer, 1, 2)]),
        new("review-backlog", "Adoption review catch-up", "Project six pending applications being reviewed.", 7, [new(SimulationAssumptionType.ApplicationsReviewed, 6, 1)]),
        new("profile-campaign", "Profile improvement campaign", "Project completion of five dog profiles.", 14, [new(SimulationAssumptionType.ProfileImprovement, 5, 3)]),
        new("notification-failure", "Notification processing failure", "Project five additional failed notifications.", 7, [new(SimulationAssumptionType.NotificationFailuresAdded, 5, 1)])
    ];

    public Task<IReadOnlyList<SimulationTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Templates);

    public async Task<SimulationBaselineDto> GetBaselineAsync(SimulationRunRequestDto request, SimulationAccessContext access, CancellationToken cancellationToken = default)
    {
        validation.Validate(request);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureScopeAsync(db, request, access, cancellationToken);

        var shelterQuery = db.Shelters.AsNoTracking().AsQueryable();
        if (request.ScopeType == SimulationScopeType.Shelter)
            shelterQuery = shelterQuery.Where(shelter => shelter.Id == request.ShelterId);

        var shelters = await shelterQuery
            .Select(shelter => new { shelter.Id, shelter.Name, shelter.DogCapacity, shelter.ReservedEmergencySpaces, shelter.ApplicationUserId })
            .ToListAsync(cancellationToken);
        if (shelters.Count == 0) throw new KeyNotFoundException("The selected shelter was not found.");

        var shelterIds = shelters.Select(item => item.Id).ToList();
        var shelterUserIds = shelters.Where(item => item.ApplicationUserId != null).Select(item => item.ApplicationUserId!).ToList();
        var now = DateTime.UtcNow;
        var activeDogStatuses = new[] { DogStatus.Available, DogStatus.Reserved, DogStatus.InTreatment };
        var openTaskStatuses = new[] { VolunteerTaskStatus.Open, VolunteerTaskStatus.Assigned, VolunteerTaskStatus.InProgress };
        var failedStatuses = new[] { NotificationOutboxStatus.Failed, NotificationOutboxStatus.DeadLetter };

        var currentDogs = await db.Dogs.CountAsync(dog => shelterIds.Contains(dog.ShelterId) && activeDogStatuses.Contains(dog.Status), cancellationToken);
        var specialNeeds = await db.Dogs.CountAsync(dog => shelterIds.Contains(dog.ShelterId) && dog.Status == DogStatus.InTreatment, cancellationToken);
        var activeVolunteers = await db.VolunteerProfiles.CountAsync(profile => profile.IsActive && (request.ScopeType == SimulationScopeType.Platform || (profile.PreferredShelterId.HasValue && shelterIds.Contains(profile.PreferredShelterId.Value))), cancellationToken);
        var openTasks = await db.VolunteerTasks.CountAsync(task => shelterIds.Contains(task.ShelterId) && openTaskStatuses.Contains(task.Status), cancellationToken);
        var overdueTasks = await db.VolunteerTasks.CountAsync(task => shelterIds.Contains(task.ShelterId) && openTaskStatuses.Contains(task.Status) && (task.DueAtUtc ?? task.ScheduledEndUtc) < now, cancellationToken);
        var pendingApplications = await db.AdoptionRequests.CountAsync(requestEntity => shelterIds.Contains(requestEntity.Dog!.ShelterId) && requestEntity.Status == AdoptionRequestStatus.Pending, cancellationToken);
        var incompleteProfiles = await db.Dogs.CountAsync(dog => shelterIds.Contains(dog.ShelterId) && activeDogStatuses.Contains(dog.Status) &&
            (dog.Description == null || dog.BehaviorDescription == null || dog.MedicalStatus == null || !dog.Images.Any()), cancellationToken);
        var incomingTransfers = await db.DogTransferRequests.CountAsync(transfer => shelterIds.Contains(transfer.DestinationShelterId) && transfer.Status == DogTransferStatus.Pending, cancellationToken);
        var outgoingTransfers = await db.DogTransferRequests.CountAsync(transfer => shelterIds.Contains(transfer.SourceShelterId) && transfer.Status == DogTransferStatus.Pending, cancellationToken);
        var failedNotifications = await db.NotificationOutboxMessages.CountAsync(message => failedStatuses.Contains(message.Status) &&
            (request.ScopeType == SimulationScopeType.Platform || (message.RecipientUserId != null && shelterUserIds.Contains(message.RecipientUserId))), cancellationToken);

        return new SimulationBaselineDto(
            request.ScopeType == SimulationScopeType.Shelter ? shelters[0].Id : null,
            request.ScopeType == SimulationScopeType.Shelter ? shelters[0].Name : "PawConnect platform",
            request.ScopeType,
            shelters.Sum(item => Math.Max(1, item.DogCapacity)),
            shelters.Sum(item => Math.Max(0, item.ReservedEmergencySpaces)),
            currentDogs,
            specialNeeds,
            activeVolunteers,
            openTasks,
            overdueTasks,
            pendingApplications,
            incompleteProfiles,
            incomingTransfers,
            outgoingTransfers,
            failedNotifications,
            now);
    }

    public async Task<SimulationResultDto> RunAsync(SimulationRunRequestDto request, SimulationAccessContext access, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        logger.LogInformation("Simulation started for scope {ScopeType}, shelter {ShelterId}, horizon {HorizonDays}, assumptions {AssumptionCount}.", request.ScopeType, request.ShelterId, request.HorizonDays, request.Assumptions.Count);
        try
        {
            var baseline = await GetBaselineAsync(request, access, cancellationToken);
            var result = await engine.RunAsync(baseline, request, access.IsAdmin, cancellationToken);
            watch.Stop();
            logger.LogInformation("Simulation completed with engine {EngineVersion} in {DurationMilliseconds} ms. Risks: baseline {BaselineRiskCount}, projected {ProjectedRiskCount}; recommendations {RecommendationCount}.", result.EngineVersion, watch.ElapsedMilliseconds, result.BaselineRisks.Count, result.ProjectedRisks.Count, result.Recommendations.Count);
            return result;
        }
        catch (Exception ex)
        {
            watch.Stop();
            logger.LogWarning(ex, "Simulation failed for scope {ScopeType}, shelter {ShelterId}, horizon {HorizonDays}, after {DurationMilliseconds} ms.", request.ScopeType, request.ShelterId, request.HorizonDays, watch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<SimulationSavedRunDto> SaveAndRunAsync(SimulationSaveRequestDto request, SimulationAccessContext access, CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(request.Name);
        var watch = Stopwatch.StartNew();
        var result = await RunAsync(request.Request, access, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        ShelterSimulationScenario scenario;
        if (request.ScenarioId.HasValue)
        {
            scenario = await GetScopedScenarioEntityAsync(db, request.ScenarioId.Value, access, cancellationToken);
            scenario.Name = name;
            scenario.Description = NormalizeDescription(request.Description);
            scenario.ShelterId = request.Request.ShelterId;
            scenario.ScopeType = request.Request.ScopeType;
            scenario.HorizonDays = request.Request.HorizonDays;
            scenario.AssumptionsJson = JsonSerializer.Serialize(request.Request.Assumptions, JsonOptions);
            scenario.IsPinned = request.IsPinned;
            scenario.Status = SimulationScenarioStatus.Completed;
            scenario.UpdatedAtUtc = DateTime.UtcNow;
            scenario.LastRunAtUtc = DateTime.UtcNow;
        }
        else
        {
            scenario = new ShelterSimulationScenario
            {
                Name = name,
                Description = NormalizeDescription(request.Description),
                CreatedByUserId = access.UserId,
                ShelterId = request.Request.ShelterId,
                ScopeType = request.Request.ScopeType,
                HorizonDays = request.Request.HorizonDays,
                Status = SimulationScenarioStatus.Completed,
                AssumptionsJson = JsonSerializer.Serialize(request.Request.Assumptions, JsonOptions),
                IsPinned = request.IsPinned,
                LastRunAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.ShelterSimulationScenarios.Add(scenario);
        }

        watch.Stop();
        var run = BuildRun(scenario, access.UserId, result, watch.ElapsedMilliseconds);
        db.ShelterSimulationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        await auditLogService.LogUserActionAsync("SimulationScenarioSaved", nameof(ShelterSimulationScenario), scenario.Id.ToString(), $"Simulation scenario saved: {scenario.Name}.", new { scenario.ScopeType, scenario.ShelterId, scenario.HorizonDays, AssumptionCount = request.Request.Assumptions.Count }, eventType: "Simulation");
        return new SimulationSavedRunDto(scenario.Id, run.Id, result);
    }

    public async Task<IReadOnlyList<SimulationScenarioListItemDto>> GetScenariosAsync(SimulationAccessContext access, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ShelterSimulationScenarios.AsNoTracking().Include(item => item.Shelter).AsQueryable();
        query = access.IsAdmin ? query : query.Where(item => item.ShelterId == access.ShelterId && item.ScopeType == SimulationScopeType.Shelter);
        var entities = await query.OrderByDescending(item => item.IsPinned).ThenByDescending(item => item.UpdatedAtUtc).ToListAsync(cancellationToken);
        return entities.Select(ToDto).ToList();
    }

    public async Task<SimulationScenarioListItemDto?> GetScenarioAsync(int scenarioId, SimulationAccessContext access, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ShelterSimulationScenarios.AsNoTracking().Include(item => item.Shelter).Where(item => item.Id == scenarioId);
        if (!access.IsAdmin) query = query.Where(item => item.ShelterId == access.ShelterId && item.ScopeType == SimulationScopeType.Shelter);
        var entity = await query.FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task RenameAsync(int scenarioId, string name, SimulationAccessContext access, CancellationToken cancellationToken = default) =>
        await UpdateScenarioAsync(scenarioId, access, entity => entity.Name = NormalizeName(name), cancellationToken);

    public async Task SetPinnedAsync(int scenarioId, bool isPinned, SimulationAccessContext access, CancellationToken cancellationToken = default) =>
        await UpdateScenarioAsync(scenarioId, access, entity => entity.IsPinned = isPinned, cancellationToken);

    public async Task DeleteAsync(int scenarioId, SimulationAccessContext access, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await GetScopedScenarioEntityAsync(db, scenarioId, access, cancellationToken);
        if (entity.IsTemplate) throw new InvalidOperationException("Built-in scenario templates cannot be deleted.");
        db.ShelterSimulationScenarios.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SimulationSavedRunDto> RerunAsync(int scenarioId, SimulationAccessContext access, CancellationToken cancellationToken = default)
    {
        var scenario = await GetScenarioAsync(scenarioId, access, cancellationToken) ?? throw new KeyNotFoundException("The scenario was not found.");
        return await SaveAndRunAsync(new SimulationSaveRequestDto(scenario.Id, scenario.Name, scenario.Description, new SimulationRunRequestDto(scenario.ShelterId, scenario.ScopeType, scenario.HorizonDays, scenario.Assumptions), scenario.IsPinned), access, cancellationToken);
    }

    public async Task<SimulationComparisonDto> CompareAsync(int firstScenarioId, int secondScenarioId, SimulationAccessContext access, CancellationToken cancellationToken = default)
    {
        if (firstScenarioId == secondScenarioId) throw new ArgumentException("Select two different scenarios to compare.");
        var first = await GetScenarioAsync(firstScenarioId, access, cancellationToken) ?? throw new KeyNotFoundException("The first scenario was not found.");
        var second = await GetScenarioAsync(secondScenarioId, access, cancellationToken) ?? throw new KeyNotFoundException("The second scenario was not found.");
        var firstResult = await RunAsync(new SimulationRunRequestDto(first.ShelterId, first.ScopeType, first.HorizonDays, first.Assumptions), access, cancellationToken);
        var secondResult = await RunAsync(new SimulationRunRequestDto(second.ShelterId, second.ScopeType, second.HorizonDays, second.Assumptions), access, cancellationToken);
        var delta = secondResult.ProjectedWorkloadScore - firstResult.ProjectedWorkloadScore;
        var summary = delta == 0 ? "Both scenarios project the same workload score." : delta > 0 ? $"{second.Name} projects {delta} more workload points than {first.Name}." : $"{second.Name} projects {Math.Abs(delta)} fewer workload points than {first.Name}.";
        return new SimulationComparisonDto(first, second, firstResult, secondResult, summary);
    }

    private static ShelterSimulationRun BuildRun(ShelterSimulationScenario scenario, string userId, SimulationResultDto result, long duration) => new()
    {
        Scenario = scenario,
        RunByUserId = userId,
        ShelterId = result.Baseline.ShelterId,
        HorizonDays = result.HorizonDays,
        BaselineSnapshotJson = JsonSerializer.Serialize(result.Baseline, JsonOptions),
        AssumptionsSnapshotJson = JsonSerializer.Serialize(result.AppliedAssumptions, JsonOptions),
        ResultSummaryJson = JsonSerializer.Serialize(result, JsonOptions),
        RiskDeltaJson = JsonSerializer.Serialize(result.RiskChanges, JsonOptions),
        CapacityDeltaJson = JsonSerializer.Serialize(new { result.Baseline.CurrentDogs, result.Baseline.DogCapacity, result.ProjectedState.AvailableNormalSpaces, result.ProjectedState.OccupancyPercent }, JsonOptions),
        RecommendationSummaryJson = JsonSerializer.Serialize(result.Recommendations, JsonOptions),
        StartedAtUtc = result.GeneratedAtUtc.AddMilliseconds(-duration),
        CompletedAtUtc = result.GeneratedAtUtc,
        DurationMilliseconds = duration,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static SimulationScenarioListItemDto ToDto(ShelterSimulationScenario entity) => new(entity.Id, entity.Name, entity.Description, entity.ShelterId, entity.Shelter?.Name, entity.ScopeType, entity.HorizonDays, entity.Status, DeserializeAssumptions(entity.AssumptionsJson), entity.IsPinned, entity.IsTemplate, entity.LastRunAtUtc, entity.UpdatedAtUtc);
    private static IReadOnlyList<SimulationAssumptionDto> DeserializeAssumptions(string json) => JsonSerializer.Deserialize<List<SimulationAssumptionDto>>(json, JsonOptions) ?? [];

    private static async Task EnsureScopeAsync(ApplicationDbContext db, SimulationRunRequestDto request, SimulationAccessContext access, CancellationToken cancellationToken)
    {
        if (request.ScopeType == SimulationScopeType.Platform)
        {
            if (!access.IsAdmin) throw new UnauthorizedAccessException("Only administrators can run platform simulations.");
            return;
        }

        if (!request.ShelterId.HasValue) throw new ArgumentException("A shelter is required.");
        if (!access.IsAdmin && access.ShelterId != request.ShelterId) throw new UnauthorizedAccessException("Shelter users can simulate only their own shelter.");
        var exists = await db.Shelters.AnyAsync(shelter => shelter.Id == request.ShelterId && (access.IsAdmin || shelter.ApplicationUserId == access.UserId), cancellationToken);
        if (!exists) throw new UnauthorizedAccessException("The selected shelter is outside the current user's scope.");
    }

    private static async Task<ShelterSimulationScenario> GetScopedScenarioEntityAsync(ApplicationDbContext db, int id, SimulationAccessContext access, CancellationToken cancellationToken)
    {
        var query = db.ShelterSimulationScenarios.Where(item => item.Id == id);
        if (!access.IsAdmin) query = query.Where(item => item.ShelterId == access.ShelterId && item.ScopeType == SimulationScopeType.Shelter);
        return await query.FirstOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("The scenario was not found.");
    }

    private async Task UpdateScenarioAsync(int id, SimulationAccessContext access, Action<ShelterSimulationScenario> update, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await GetScopedScenarioEntityAsync(db, id, access, cancellationToken);
        update(entity);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeName(string name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length is < 3 or > 140) throw new ArgumentException("Scenario name must be between 3 and 140 characters.");
        return value;
    }

    private static string? NormalizeDescription(string? description)
    {
        var value = description?.Trim();
        if (value?.Length > 800) throw new ArgumentException("Scenario description cannot exceed 800 characters.");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
