using PawConnect.Entities;

namespace PawConnect.Services.Simulation;

public sealed class SimulationRecommendationService : ISimulationRecommendationService
{
    public IReadOnlyList<SimulationRecommendationDto> Build(SimulationBaselineDto baseline, SimulationStateDto projected, IReadOnlyList<SimulationRiskChangeDto> changes, bool isAdmin)
    {
        var prefix = isAdmin ? "/admin" : "/shelter";
        var role = isAdmin ? "Admin" : "Shelter";
        var recommendations = new List<SimulationRecommendationDto>();

        if (projected.AvailableNormalSpaces <= 1)
            recommendations.Add(new("protect-capacity", "Protect emergency capacity", "Review intake timing and outgoing transfer options before normal capacity is exhausted.", $"{prefix}/transfers", role, 100));
        if (changes.Any(change => change.Key == "VolunteerBacklog" && change.Impact is SimulationImpactType.NewRisk or SimulationImpactType.EscalatedRisk))
            recommendations.Add(new("rebalance-volunteers", "Rebalance volunteer coverage", "Assign high-priority work first and reschedule work that is not time-sensitive.", $"{prefix}/volunteer-tasks", role, 90));
        if (projected.PendingApplications >= 5)
            recommendations.Add(new("review-applications", "Schedule an application review block", "Reduce the pending queue before adding more intake or transfer work.", $"{prefix}/adoption-requests", role, 80));
        if (projected.IncompleteProfiles > 0)
            recommendations.Add(new("improve-profiles", "Complete high-priority dog profiles", "Focus first on available dogs with missing photos, behavior, compatibility, or medical information.", $"{prefix}/dogs", role, 70));
        if (projected.FailedNotifications > 0 && isAdmin)
            recommendations.Add(new("restore-delivery", "Review failed notifications", "Resolve delivery failures before relying on scheduled communication.", "/admin/notification-outbox", role, 65));
        if (recommendations.Count == 0)
            recommendations.Add(new("monitor", "Monitor the projected plan", "No major deterioration was detected. Re-run the scenario when assumptions or shelter conditions change.", $"{prefix}/intelligence", role, 30));

        return recommendations.OrderByDescending(item => item.Priority).ToList();
    }
}
