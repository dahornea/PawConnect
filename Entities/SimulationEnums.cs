namespace PawConnect.Entities;

public enum SimulationScopeType
{
    Shelter = 0,
    Platform = 1
}

public enum SimulationScenarioStatus
{
    Draft = 0,
    Completed = 1,
    Archived = 2
}

public enum SimulationAssumptionType
{
    DogIntake = 0,
    VolunteerUnavailable = 1,
    VolunteerAdded = 2,
    IncomingTransfer = 3,
    OutgoingTransfer = 4,
    ApplicationsReviewed = 5,
    NewApplications = 6,
    ProfileImprovement = 7,
    NotificationFailuresAdded = 8,
    NotificationBacklogCleared = 9,
    TemporaryCapacityChange = 10,
    TemporaryCapacityUnavailable = 11
}

public enum SimulationImpactType
{
    NewRisk = 0,
    EscalatedRisk = 1,
    UnchangedRisk = 2,
    ReducedRisk = 3,
    ResolvedRisk = 4,
    NewOpportunity = 5
}
