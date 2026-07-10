using PawConnect.Entities;

namespace PawConnect.Services.Simulation;

public sealed class SimulationValidationService : ISimulationValidationService
{
    public void Validate(SimulationRunRequestDto request)
    {
        if (request.HorizonDays is < 1 or > 90) throw new ArgumentException("The simulation horizon must be between 1 and 90 days.");
        if (request.ScopeType == SimulationScopeType.Shelter && !request.ShelterId.HasValue) throw new ArgumentException("A shelter is required for a shelter simulation.");
        if (request.Assumptions.Count > 30) throw new ArgumentException("A simulation can contain at most 30 assumptions.");
        if (request.Assumptions.GroupBy(item => new { item.Type, item.EffectiveDay }).Any(group => group.Count() > 1))
            throw new ArgumentException("Combine duplicate assumptions of the same type and effective day into one quantity.");
        foreach (var assumption in request.Assumptions)
        {
            if (assumption.Quantity is < 1 or > 1000) throw new ArgumentException("Each assumption quantity must be between 1 and 1000.");
            if (assumption.EffectiveDay < 1 || assumption.EffectiveDay > request.HorizonDays) throw new ArgumentException("Each assumption must take effect within the selected horizon.");
        }
    }
}
