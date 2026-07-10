using PawConnect.Entities;

namespace PawConnect.Services.Simulation;

public sealed class DogOperationsAssumptionApplier : ISimulationAssumptionApplier
{
    public bool Supports(SimulationAssumptionType type) => type is SimulationAssumptionType.DogIntake or SimulationAssumptionType.IncomingTransfer or SimulationAssumptionType.OutgoingTransfer;
    public SimulationStateDto Apply(SimulationStateDto state, SimulationAssumptionDto assumption) => assumption.Type switch
    {
        SimulationAssumptionType.DogIntake => state with { CurrentDogs = state.CurrentDogs + assumption.Quantity },
        SimulationAssumptionType.IncomingTransfer => state with { CurrentDogs = state.CurrentDogs + assumption.Quantity, IncomingTransfers = state.IncomingTransfers + assumption.Quantity },
        SimulationAssumptionType.OutgoingTransfer => state with { CurrentDogs = Math.Max(0, state.CurrentDogs - assumption.Quantity), OutgoingTransfers = state.OutgoingTransfers + assumption.Quantity },
        _ => state
    };
}

public sealed class VolunteerCapacityAssumptionApplier : ISimulationAssumptionApplier
{
    public bool Supports(SimulationAssumptionType type) => type is SimulationAssumptionType.VolunteerUnavailable or SimulationAssumptionType.VolunteerAdded;
    public SimulationStateDto Apply(SimulationStateDto state, SimulationAssumptionDto assumption) => assumption.Type switch
    {
        SimulationAssumptionType.VolunteerUnavailable => state with { ActiveVolunteers = Math.Max(0, state.ActiveVolunteers - assumption.Quantity) },
        SimulationAssumptionType.VolunteerAdded => state with { ActiveVolunteers = state.ActiveVolunteers + assumption.Quantity },
        _ => state
    };
}

public sealed class AdoptionWorkflowAssumptionApplier : ISimulationAssumptionApplier
{
    public bool Supports(SimulationAssumptionType type) => type is SimulationAssumptionType.ApplicationsReviewed or SimulationAssumptionType.NewApplications;
    public SimulationStateDto Apply(SimulationStateDto state, SimulationAssumptionDto assumption) => assumption.Type switch
    {
        SimulationAssumptionType.ApplicationsReviewed => state with { PendingApplications = Math.Max(0, state.PendingApplications - assumption.Quantity) },
        SimulationAssumptionType.NewApplications => state with { PendingApplications = state.PendingApplications + assumption.Quantity },
        _ => state
    };
}

public sealed class ProfileQualityAssumptionApplier : ISimulationAssumptionApplier
{
    public bool Supports(SimulationAssumptionType type) => type == SimulationAssumptionType.ProfileImprovement;
    public SimulationStateDto Apply(SimulationStateDto state, SimulationAssumptionDto assumption) => state with { IncompleteProfiles = Math.Max(0, state.IncompleteProfiles - assumption.Quantity) };
}

public sealed class NotificationReliabilityAssumptionApplier : ISimulationAssumptionApplier
{
    public bool Supports(SimulationAssumptionType type) => type is SimulationAssumptionType.NotificationFailuresAdded or SimulationAssumptionType.NotificationBacklogCleared;
    public SimulationStateDto Apply(SimulationStateDto state, SimulationAssumptionDto assumption) => assumption.Type switch
    {
        SimulationAssumptionType.NotificationFailuresAdded => state with { FailedNotifications = state.FailedNotifications + assumption.Quantity },
        SimulationAssumptionType.NotificationBacklogCleared => state with { FailedNotifications = Math.Max(0, state.FailedNotifications - assumption.Quantity) },
        _ => state
    };
}

public sealed class ShelterCapacityAssumptionApplier : ISimulationAssumptionApplier
{
    public bool Supports(SimulationAssumptionType type) => type is SimulationAssumptionType.TemporaryCapacityChange or SimulationAssumptionType.TemporaryCapacityUnavailable;
    public SimulationStateDto Apply(SimulationStateDto state, SimulationAssumptionDto assumption) => assumption.Type switch
    {
        SimulationAssumptionType.TemporaryCapacityChange => state with { DogCapacity = state.DogCapacity + assumption.Quantity },
        SimulationAssumptionType.TemporaryCapacityUnavailable => state with { DogCapacity = Math.Max(1, state.DogCapacity - assumption.Quantity) },
        _ => state
    };
}
