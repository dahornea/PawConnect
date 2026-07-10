using PawConnect.Entities;

namespace PawConnect.Services.Simulation;

public sealed record SimulationAccessContext(string UserId, bool IsAdmin, int? ShelterId = null);
public sealed record SimulationAssumptionDto(SimulationAssumptionType Type, int Quantity, int EffectiveDay = 1, string? Notes = null);
public sealed record SimulationBaselineDto(int? ShelterId, string ScopeName, SimulationScopeType ScopeType, int DogCapacity, int ReservedEmergencySpaces, int CurrentDogs, int SpecialNeedsDogs, int ActiveVolunteers, int OpenVolunteerTasks, int OverdueVolunteerTasks, int PendingApplications, int IncompleteProfiles, int IncomingTransfers, int OutgoingTransfers, int FailedNotifications, DateTime CapturedAtUtc);
public sealed record SimulationStateDto(int DogCapacity, int ReservedEmergencySpaces, int CurrentDogs, int SpecialNeedsDogs, int ActiveVolunteers, int OpenVolunteerTasks, int OverdueVolunteerTasks, int PendingApplications, int IncompleteProfiles, int IncomingTransfers, int OutgoingTransfers, int FailedNotifications)
{
    public int NormalCapacity => Math.Max(0, DogCapacity - ReservedEmergencySpaces);
    public int AvailableNormalSpaces => NormalCapacity - CurrentDogs;
    public int OccupancyPercent => DogCapacity == 0 ? 100 : (int)Math.Round(CurrentDogs * 100d / DogCapacity);
}
public sealed record SimulationDimensionResultDto(string Key, string Label, int BaselineScore, int ProjectedScore, string BaselineLabel, string ProjectedLabel, string Explanation);
public sealed record SimulationRiskDto(string Key, string Title, IntelligenceCategory Category, IntelligenceSeverity Severity, int Score, string Explanation, IReadOnlyList<string> Evidence, int EffectiveDay = 0);
public sealed record SimulationRiskChangeDto(string Key, string Title, SimulationImpactType Impact, SimulationRiskDto? Baseline, SimulationRiskDto? Projected, string Explanation);
public sealed record SimulationRecommendationDto(string Key, string Title, string Explanation, string Route, string RequiredRole, int Priority);
public sealed record SimulationTimelinePointDto(int Day, int CurrentDogs, int AvailableSpaces, int WorkloadScore, int RiskCount, IReadOnlyList<string> AppliedAssumptions);
public sealed record SimulationWorkloadFactorDto(string Label, int BaselineContribution, int ProjectedContribution, string Formula);
public sealed record SimulationResultDto(SimulationBaselineDto Baseline, SimulationStateDto ProjectedState, int HorizonDays, int BaselineWorkloadScore, int ProjectedWorkloadScore, IReadOnlyList<SimulationDimensionResultDto> Dimensions, IReadOnlyList<SimulationWorkloadFactorDto> WorkloadFactors, IReadOnlyList<SimulationRiskDto> BaselineRisks, IReadOnlyList<SimulationRiskDto> ProjectedRisks, IReadOnlyList<SimulationRiskChangeDto> RiskChanges, IReadOnlyList<SimulationRecommendationDto> Recommendations, IReadOnlyList<SimulationTimelinePointDto> Timeline, IReadOnlyList<SimulationAssumptionDto> AppliedAssumptions, DateTime GeneratedAtUtc, string EngineVersion = "1.0");
public sealed record SimulationRunRequestDto(int? ShelterId, SimulationScopeType ScopeType, int HorizonDays, IReadOnlyList<SimulationAssumptionDto> Assumptions);
public sealed record SimulationSaveRequestDto(int? ScenarioId, string Name, string? Description, SimulationRunRequestDto Request, bool IsPinned = false);
public sealed record SimulationScenarioListItemDto(int Id, string Name, string? Description, int? ShelterId, string? ShelterName, SimulationScopeType ScopeType, int HorizonDays, SimulationScenarioStatus Status, IReadOnlyList<SimulationAssumptionDto> Assumptions, bool IsPinned, bool IsTemplate, DateTime? LastRunAtUtc, DateTime UpdatedAtUtc);
public sealed record SimulationSavedRunDto(int ScenarioId, int RunId, SimulationResultDto Result);
public sealed record SimulationComparisonDto(SimulationScenarioListItemDto First, SimulationScenarioListItemDto Second, SimulationResultDto FirstResult, SimulationResultDto SecondResult, string Summary);
public sealed record SimulationTemplateDto(string Key, string Name, string Description, int HorizonDays, IReadOnlyList<SimulationAssumptionDto> Assumptions);
