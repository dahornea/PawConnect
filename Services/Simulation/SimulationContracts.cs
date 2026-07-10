using PawConnect.Entities;

namespace PawConnect.Services.Simulation;

public interface ISimulationAssumptionApplier { bool Supports(SimulationAssumptionType type); SimulationStateDto Apply(SimulationStateDto state, SimulationAssumptionDto assumption); }
public interface ISimulationValidationService { void Validate(SimulationRunRequestDto request); }
public interface ISimulationImpactAnalyzer { Task<IReadOnlyList<SimulationRiskDto>> AnalyzeAsync(SimulationBaselineDto baseline, SimulationStateDto state, int effectiveDay, CancellationToken cancellationToken = default); }
public interface ISimulationRecommendationService { IReadOnlyList<SimulationRecommendationDto> Build(SimulationBaselineDto baseline, SimulationStateDto projected, IReadOnlyList<SimulationRiskChangeDto> changes, bool isAdmin); }
public interface ISimulationEngine { Task<SimulationResultDto> RunAsync(SimulationBaselineDto baseline, SimulationRunRequestDto request, bool isAdmin, CancellationToken cancellationToken = default); }
public interface IShelterSimulationService
{
    Task<IReadOnlyList<SimulationTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<SimulationBaselineDto> GetBaselineAsync(SimulationRunRequestDto request, SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task<SimulationResultDto> RunAsync(SimulationRunRequestDto request, SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task<SimulationSavedRunDto> SaveAndRunAsync(SimulationSaveRequestDto request, SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimulationScenarioListItemDto>> GetScenariosAsync(SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task<SimulationScenarioListItemDto?> GetScenarioAsync(int scenarioId, SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task RenameAsync(int scenarioId, string name, SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task SetPinnedAsync(int scenarioId, bool isPinned, SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task DeleteAsync(int scenarioId, SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task<SimulationSavedRunDto> RerunAsync(int scenarioId, SimulationAccessContext access, CancellationToken cancellationToken = default);
    Task<SimulationComparisonDto> CompareAsync(int firstScenarioId, int secondScenarioId, SimulationAccessContext access, CancellationToken cancellationToken = default);
}
