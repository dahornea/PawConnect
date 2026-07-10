using PawConnect.Entities;
using PawConnect.Services.Intelligence;

namespace PawConnect.Services.Simulation;

public sealed class SimulationImpactAnalyzer(IIntelligenceRule intelligenceRule) : ISimulationImpactAnalyzer
{
    public async Task<IReadOnlyList<SimulationRiskDto>> AnalyzeAsync(
        SimulationBaselineDto baseline,
        SimulationStateDto state,
        int effectiveDay,
        CancellationToken cancellationToken = default)
    {
        var audience = baseline.ScopeType == SimulationScopeType.Platform ? IntelligenceAudienceType.Admin : IntelligenceAudienceType.Shelter;
        var context = new IntelligenceContext(audience, null, baseline.ShelterId, baseline.CapturedAtUtc.AddDays(effectiveDay));
        var signals = BuildSignals(baseline, state, context.UtcNow);
        var candidates = await intelligenceRule.EvaluateAsync(new IntelligenceEvaluationContext(context, signals), cancellationToken);

        return candidates
            .Where(candidate => candidate.PriorityScore >= 25)
            .Select(candidate => new SimulationRiskDto(
                candidate.InsightType,
                candidate.Title,
                candidate.Category,
                candidate.Severity,
                candidate.PriorityScore,
                candidate.Explanation,
                candidate.Evidence,
                effectiveDay))
            .OrderByDescending(risk => risk.Score)
            .ThenBy(risk => risk.Title)
            .ToList();
    }

    private static IReadOnlyList<IntelligenceSignal> BuildSignals(SimulationBaselineDto baseline, SimulationStateDto state, DateTime observedAtUtc)
    {
        var role = baseline.ScopeType == SimulationScopeType.Platform ? "Admin" : "Shelter";
        var routePrefix = baseline.ScopeType == SimulationScopeType.Platform ? "/admin" : "/shelter";
        var workload = SimulationScoring.Workload(state);
        var signals = new List<IntelligenceSignal>
        {
            Signal(
                "CapacityPressure",
                IntelligenceCategory.Workload,
                "Shelter capacity pressure",
                $"Projected occupancy is {state.OccupancyPercent}% ({state.CurrentDogs} dogs across {state.DogCapacity} spaces).",
                "Operating near or beyond normal capacity reduces room for safe intake and emergency cases.",
                "occupancy falls below 85%",
                "Occupancy reaches 85% or normal spaces fall below zero",
                [$"Normal spaces available: {state.AvailableNormalSpaces}", $"Emergency spaces reserved: {state.ReservedEmergencySpaces}"],
                [new("Occupancy", SimulationScoring.CapacityPressure(state), $"Projected occupancy is {state.OccupancyPercent}%"), new("No normal capacity", state.AvailableNormalSpaces < 0 ? 28 : state.AvailableNormalSpaces == 0 ? 16 : 0, $"Normal spaces available: {state.AvailableNormalSpaces}")],
                new("open-dogs", "Review shelter dogs", "Review current dog statuses and intake readiness.", "Navigate", $"{routePrefix}/dogs", role, IsPrimary: true),
                baseline,
                observedAtUtc),
            Signal(
                "OperationalWorkload",
                IntelligenceCategory.Workload,
                "Operational workload may exceed team capacity",
                $"The projected workload index is {workload}/100 ({SimulationScoring.WorkloadLabel(workload)}).",
                "Sustained workload can delay dog care, application review, and routine shelter operations.",
                "the workload index returns below 40",
                "Workload score reflects dogs, special needs, tasks, applications, transfers, notifications, and volunteer capacity",
                [$"Active volunteers: {state.ActiveVolunteers}", $"Open tasks: {state.OpenVolunteerTasks}", $"Pending applications: {state.PendingApplications}"],
                [new("Workload index", workload, $"Deterministic projected workload is {workload}/100")],
                new("open-tasks", "Review volunteer workload", "Review open and overdue volunteer tasks.", "Navigate", $"{routePrefix}/volunteer-tasks", role, IsPrimary: true),
                baseline,
                observedAtUtc),
            Signal(
                "ApplicationBacklog",
                IntelligenceCategory.ApplicationReview,
                "Adoption application backlog needs review",
                $"There are {state.PendingApplications} projected pending applications.",
                "A growing queue can delay adopter communication and shelter decisions.",
                "pending applications fall below five",
                "Five or more pending applications",
                [$"Pending applications: {state.PendingApplications}"],
                [new("Queue size", Math.Min(70, state.PendingApplications * 7), $"{state.PendingApplications} applications await review")],
                new("review-applications", "Review applications", "Open the adoption request workspace.", "Navigate", $"{routePrefix}/adoption-requests", role, IsPrimary: true),
                baseline,
                observedAtUtc),
            Signal(
                "VolunteerBacklog",
                IntelligenceCategory.Volunteer,
                "Volunteer coverage may be insufficient",
                $"{state.OpenVolunteerTasks} tasks are open, including {state.OverdueVolunteerTasks} overdue, with {state.ActiveVolunteers} active volunteers.",
                "Uncovered work can affect feeding, walking, transport, and appointment preparation.",
                "overdue work is cleared and open tasks align with active volunteers",
                "Open and overdue tasks exceed available volunteer coverage",
                [$"Open tasks: {state.OpenVolunteerTasks}", $"Overdue tasks: {state.OverdueVolunteerTasks}", $"Active volunteers: {state.ActiveVolunteers}"],
                [new("Open work", Math.Min(45, state.OpenVolunteerTasks * 4), $"{state.OpenVolunteerTasks} tasks remain open"), new("Overdue work", Math.Min(35, state.OverdueVolunteerTasks * 10), $"{state.OverdueVolunteerTasks} tasks are overdue"), new("Coverage gap", state.OpenVolunteerTasks > state.ActiveVolunteers * 3 ? 20 : 0, "Task volume is compared with active volunteers")],
                new("assign-volunteers", "Review volunteer tasks", "Assign, reschedule, or close outstanding tasks.", "Navigate", $"{routePrefix}/volunteer-tasks", role, IsPrimary: true),
                baseline,
                observedAtUtc),
            Signal(
                "ProfileQuality",
                IntelligenceCategory.DogProfileQuality,
                "Dog profile quality limits adoption readiness",
                $"{state.IncompleteProfiles} projected dog profiles remain incomplete.",
                "Incomplete profiles reduce public confidence and weaken matching quality.",
                "the incomplete profile count reaches zero",
                "One or more active dog profiles have important missing information",
                [$"Incomplete profiles: {state.IncompleteProfiles}"],
                [new("Profile gaps", Math.Min(75, state.IncompleteProfiles * 12), $"{state.IncompleteProfiles} profiles need work")],
                new("complete-profiles", "Complete dog profiles", "Review dog photos, compatibility, medical, and care information.", "Navigate", $"{routePrefix}/dogs", role, IsPrimary: true),
                baseline,
                observedAtUtc),
            Signal(
                "NotificationReliability",
                IntelligenceCategory.Notifications,
                "Notification delivery failures may affect follow-up",
                $"The scenario projects {state.FailedNotifications} failed or dead-letter notifications.",
                "Missed notifications can delay operational and adoption communication.",
                "failed notifications are retried, cancelled, or resolved",
                "One or more failed or dead-letter notifications",
                [$"Failed notifications: {state.FailedNotifications}"],
                [new("Delivery failures", Math.Min(80, state.FailedNotifications * 10), $"{state.FailedNotifications} messages need attention")],
                new("review-outbox", "Review notification outbox", "Inspect failed deliveries and retry status.", "Navigate", "/admin/notification-outbox", "Admin", IsPrimary: true, IsAvailable: baseline.ScopeType == SimulationScopeType.Platform, UnavailableReason: "Notification outbox management is available to administrators."),
                baseline,
                observedAtUtc),
            Signal(
                "TransferReadiness",
                IntelligenceCategory.Transfer,
                "Transfer workload requires coordination",
                $"The scenario includes {state.IncomingTransfers} incoming and {state.OutgoingTransfers} outgoing transfers.",
                "Transfers affect capacity, transport planning, records, and receiving-shelter readiness.",
                "pending transfer work is completed or cancelled",
                "Three or more projected transfer actions",
                [$"Incoming: {state.IncomingTransfers}", $"Outgoing: {state.OutgoingTransfers}"],
                [new("Transfer volume", Math.Min(75, (state.IncomingTransfers + state.OutgoingTransfers) * 15), "Incoming and outgoing transfers are counted")],
                new("review-transfers", "Review transfers", "Coordinate sending and receiving shelter work.", "Navigate", $"{routePrefix}/transfers", role, IsPrimary: true),
                baseline,
                observedAtUtc)
        };

        return signals;
    }

    private static IntelligenceSignal Signal(
        string key,
        IntelligenceCategory category,
        string title,
        string summary,
        string why,
        string resolution,
        string threshold,
        IReadOnlyList<string> evidence,
        IReadOnlyList<IntelligenceScoreFactor> factors,
        RecommendedActionDto action,
        SimulationBaselineDto baseline,
        DateTime observedAtUtc) => new(
            key, category, "ScenarioSimulator", "Simulation", null, baseline.ScopeName, null, baseline.ShelterId,
            title, summary, why, resolution, threshold, evidence, factors, [action], observedAtUtc);
}
