namespace PawConnect.Services.Simulation;

public static class SimulationScoring
{
    public static int Workload(SimulationStateDto state) => Math.Clamp(
        state.CurrentDogs * 2 +
        state.SpecialNeedsDogs * 3 +
        state.OpenVolunteerTasks * 2 +
        state.OverdueVolunteerTasks * 5 +
        state.PendingApplications * 2 +
        state.IncomingTransfers * 3 +
        state.IncompleteProfiles +
        state.FailedNotifications * 2 -
        state.ActiveVolunteers * 4,
        0,
        100);

    public static string WorkloadLabel(int score) => score switch
    {
        >= 80 => "Critical",
        >= 60 => "High",
        >= 40 => "Elevated",
        _ => "Normal"
    };

    public static int CapacityPressure(SimulationStateDto state) => Math.Clamp(state.OccupancyPercent - 55, 0, 45) * 2;
}
