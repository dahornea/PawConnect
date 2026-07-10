namespace PawConnect.Entities;

public enum IntelligenceAudienceType
{
    Admin = 0,
    Shelter = 1,
    Adopter = 2
}

public enum IntelligenceCategory
{
    Adoption = 0,
    DogProfileQuality = 1,
    ApplicationReview = 2,
    Workload = 3,
    Notifications = 4,
    Transfer = 5,
    Volunteer = 6,
    PlatformHealth = 7,
    Matching = 8,
    UserNextStep = 9
}

public enum IntelligenceSeverity
{
    Informational = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum IntelligenceInsightStatus
{
    Active = 0,
    Acknowledged = 1,
    Snoozed = 2,
    Resolved = 3,
    Expired = 4
}

