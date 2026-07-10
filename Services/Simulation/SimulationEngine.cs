using PawConnect.Entities;

namespace PawConnect.Services.Simulation;

public sealed class SimulationEngine(
    IEnumerable<ISimulationAssumptionApplier> appliers,
    ISimulationValidationService validation,
    ISimulationImpactAnalyzer impactAnalyzer,
    ISimulationRecommendationService recommendationService) : ISimulationEngine
{
    public async Task<SimulationResultDto> RunAsync(SimulationBaselineDto baseline, SimulationRunRequestDto request, bool isAdmin, CancellationToken cancellationToken = default)
    {
        validation.Validate(request);
        var baselineState = ToState(baseline);
        var baselineRisks = await impactAnalyzer.AnalyzeAsync(baseline, baselineState, 0, cancellationToken);
        var firstRiskDay = baselineRisks.ToDictionary(risk => risk.Key, _ => 0);
        var state = baselineState;
        var timeline = new List<SimulationTimelinePointDto> { Point(0, state, baselineRisks.Count, []) };

        for (var day = 1; day <= request.HorizonDays; day++)
        {
            var due = request.Assumptions.Where(item => item.EffectiveDay == day).ToList();
            foreach (var assumption in due)
            {
                var applier = appliers.FirstOrDefault(item => item.Supports(assumption.Type))
                    ?? throw new InvalidOperationException($"No simulator handler exists for {assumption.Type}.");
                state = applier.Apply(state, assumption);
            }

            var dayRisks = await impactAnalyzer.AnalyzeAsync(baseline, state, day, cancellationToken);
            foreach (var risk in dayRisks)
            {
                firstRiskDay.TryAdd(risk.Key, day);
            }
            timeline.Add(Point(day, state, dayRisks.Count, due.Select(Describe).ToList()));
        }

        var projectedRisks = (await impactAnalyzer.AnalyzeAsync(baseline, state, request.HorizonDays, cancellationToken))
            .Select(risk => risk with { EffectiveDay = firstRiskDay.GetValueOrDefault(risk.Key, request.HorizonDays) })
            .ToList();
        var changes = CompareRisks(baselineRisks, projectedRisks);
        var baselineWorkload = SimulationScoring.Workload(baselineState);
        var projectedWorkload = SimulationScoring.Workload(state);
        var dimensions = BuildDimensions(baselineState, state, baselineWorkload, projectedWorkload);
        var recommendations = recommendationService.Build(baseline, state, changes, isAdmin);

        return new SimulationResultDto(baseline, state, request.HorizonDays, baselineWorkload, projectedWorkload, dimensions, BuildWorkloadFactors(baselineState, state), baselineRisks, projectedRisks, changes, recommendations, timeline, request.Assumptions, DateTime.UtcNow);
    }

    private static SimulationStateDto ToState(SimulationBaselineDto value) => new(value.DogCapacity, value.ReservedEmergencySpaces, value.CurrentDogs, value.SpecialNeedsDogs, value.ActiveVolunteers, value.OpenVolunteerTasks, value.OverdueVolunteerTasks, value.PendingApplications, value.IncompleteProfiles, value.IncomingTransfers, value.OutgoingTransfers, value.FailedNotifications);
    private static SimulationTimelinePointDto Point(int day, SimulationStateDto state, int riskCount, IReadOnlyList<string> applied) => new(day, state.CurrentDogs, state.AvailableNormalSpaces, SimulationScoring.Workload(state), riskCount, applied);
    private static string Describe(SimulationAssumptionDto assumption) => $"{assumption.Type}: {assumption.Quantity}";

    private static IReadOnlyList<SimulationRiskChangeDto> CompareRisks(IReadOnlyList<SimulationRiskDto> baseline, IReadOnlyList<SimulationRiskDto> projected)
    {
        var first = baseline.ToDictionary(item => item.Key);
        var second = projected.ToDictionary(item => item.Key);
        var keys = first.Keys.Union(second.Keys).OrderBy(key => key);
        return keys.Select(key =>
        {
            first.TryGetValue(key, out var before);
            second.TryGetValue(key, out var after);
            var impact = before is null ? SimulationImpactType.NewRisk
                : after is null ? SimulationImpactType.ResolvedRisk
                : after.Score >= before.Score + 10 ? SimulationImpactType.EscalatedRisk
                : after.Score <= before.Score - 10 ? SimulationImpactType.ReducedRisk
                : SimulationImpactType.UnchangedRisk;
            var explanation = impact switch
            {
                SimulationImpactType.NewRisk => $"New projected risk at {after!.Score}/100.",
                SimulationImpactType.ResolvedRisk => $"Baseline risk at {before!.Score}/100 is no longer projected.",
                SimulationImpactType.EscalatedRisk => $"Risk increases from {before!.Score} to {after!.Score}.",
                SimulationImpactType.ReducedRisk => $"Risk decreases from {before!.Score} to {after!.Score}.",
                _ => $"Risk remains broadly stable ({before!.Score} to {after!.Score})."
            };
            return new SimulationRiskChangeDto(key, after?.Title ?? before!.Title, impact, before, after, explanation);
        }).ToList();
    }

    private static IReadOnlyList<SimulationDimensionResultDto> BuildDimensions(SimulationStateDto before, SimulationStateDto after, int beforeWorkload, int afterWorkload) =>
    [
        new("capacity", "Capacity pressure", Math.Clamp(before.OccupancyPercent, 0, 100), Math.Clamp(after.OccupancyPercent, 0, 100), CapacityLabel(before), CapacityLabel(after), $"Normal spaces change from {before.AvailableNormalSpaces} to {after.AvailableNormalSpaces}."),
        new("workload", "Operational workload", beforeWorkload, afterWorkload, SimulationScoring.WorkloadLabel(beforeWorkload), SimulationScoring.WorkloadLabel(afterWorkload), "Includes dog care, tasks, applications, transfers, profile gaps, notifications, and volunteer capacity."),
        new("applications", "Application review", Math.Min(100, before.PendingApplications * 10), Math.Min(100, after.PendingApplications * 10), QueueLabel(before.PendingApplications), QueueLabel(after.PendingApplications), $"Pending applications change from {before.PendingApplications} to {after.PendingApplications}."),
        new("volunteers", "Volunteer coverage", CoverageScore(before), CoverageScore(after), CoverageLabel(before), CoverageLabel(after), $"Active volunteers: {before.ActiveVolunteers} to {after.ActiveVolunteers}; open tasks: {before.OpenVolunteerTasks} to {after.OpenVolunteerTasks}."),
        new("quality", "Profile readiness", ProfileScore(before), ProfileScore(after), ProfileLabel(before), ProfileLabel(after), $"Incomplete profiles change from {before.IncompleteProfiles} to {after.IncompleteProfiles}."),
        new("notifications", "Notification reliability", Math.Min(100, before.FailedNotifications * 12), Math.Min(100, after.FailedNotifications * 12), NotificationLabel(before), NotificationLabel(after), $"Failed notifications change from {before.FailedNotifications} to {after.FailedNotifications}.")
    ];

    private static IReadOnlyList<SimulationWorkloadFactorDto> BuildWorkloadFactors(SimulationStateDto before, SimulationStateDto after) =>
    [
        new("Dogs in care", before.CurrentDogs * 2, after.CurrentDogs * 2, "2 points per dog"),
        new("Special-needs dogs", before.SpecialNeedsDogs * 3, after.SpecialNeedsDogs * 3, "3 points per dog in treatment"),
        new("Open volunteer tasks", before.OpenVolunteerTasks * 2, after.OpenVolunteerTasks * 2, "2 points per open task"),
        new("Overdue volunteer tasks", before.OverdueVolunteerTasks * 5, after.OverdueVolunteerTasks * 5, "5 points per overdue task"),
        new("Pending applications", before.PendingApplications * 2, after.PendingApplications * 2, "2 points per pending application"),
        new("Incoming transfers", before.IncomingTransfers * 3, after.IncomingTransfers * 3, "3 points per incoming transfer"),
        new("Incomplete profiles", before.IncompleteProfiles, after.IncompleteProfiles, "1 point per incomplete profile"),
        new("Failed notifications", before.FailedNotifications * 2, after.FailedNotifications * 2, "2 points per failed notification"),
        new("Active volunteers", before.ActiveVolunteers * -4, after.ActiveVolunteers * -4, "subtract 4 points per active volunteer")
    ];

    private static string CapacityLabel(SimulationStateDto state) => state.OccupancyPercent switch { >= 100 => "Over capacity", >= 85 => "Limited", _ => "Available" };
    private static string QueueLabel(int count) => count switch { >= 10 => "High backlog", >= 5 => "Needs review", _ => "Manageable" };
    private static int CoverageScore(SimulationStateDto state) => Math.Clamp(state.OpenVolunteerTasks * 8 + state.OverdueVolunteerTasks * 12 - state.ActiveVolunteers * 5, 0, 100);
    private static string CoverageLabel(SimulationStateDto state) => CoverageScore(state) switch { >= 70 => "Insufficient", >= 40 => "Tight", _ => "Balanced" };
    private static int ProfileScore(SimulationStateDto state) => Math.Min(100, state.IncompleteProfiles * 12);
    private static string ProfileLabel(SimulationStateDto state) => state.IncompleteProfiles switch { 0 => "Ready", <= 3 => "Some gaps", _ => "Needs work" };
    private static string NotificationLabel(SimulationStateDto state) => state.FailedNotifications switch { 0 => "Reliable", <= 3 => "Monitor", _ => "Needs attention" };
}
