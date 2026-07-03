using PawConnect.Entities;

namespace PawConnect.Services;

public enum DogProfileQualityCategory
{
    MissingBehaviorInfo,
    MissingCompatibilityInfo,
    MissingActivityInfo,
    MissingMedicalContext,
    MissingIdealHomeInfo,
    VagueDescription,
    OverconfidentClaim,
    PotentiallyMisleadingClaim,
    TooShort,
    GoodProfileStrength
}

public enum DogProfileQualitySeverity
{
    Info,
    Low,
    Medium,
    High
}

public sealed class DogProfileQualityRequest
{
    public int? DogId { get; init; }

    public int ShelterId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int AgeYears { get; init; }

    public int AgeMonths { get; init; }

    public DogSize Size { get; init; }

    public DogStatus Status { get; init; }

    public string BreedDisplay { get; init; } = "Unknown";

    public string? CoatColor { get; init; }

    public string? Description { get; init; }

    public string? BehaviorDescription { get; init; }

    public string? MedicalStatus { get; init; }

    public string? PreferredFoodType { get; init; }

    public int? DailyFoodAmountGrams { get; init; }

    public CatCompatibility CatCompatibility { get; init; } = CatCompatibility.Unknown;

    public DogCompatibility DogCompatibility { get; init; } = DogCompatibility.Unknown;

    public ChildrenCompatibility ChildrenCompatibility { get; init; } = ChildrenCompatibility.Unknown;

    public DogActivityLevel ActivityLevel { get; init; } = DogActivityLevel.Unknown;

    public DogExperienceNeeded ExperienceNeeded { get; init; } = DogExperienceNeeded.Unknown;

    public ApartmentSuitability ApartmentSuitability { get; init; } = ApartmentSuitability.Unknown;

    public string? CompatibilityNotes { get; init; }
}

public sealed record DogProfileQualityIssue(
    DogProfileQualityCategory Category,
    DogProfileQualitySeverity Severity,
    string Message,
    string? FieldName = null,
    string? SuggestedAction = null);

public sealed record DogProfileQualitySuggestion(
    string FieldName,
    string SuggestedText,
    string? Rationale = null);

public sealed record DogProfileRewriteSuggestion(
    string? Title,
    string? Description,
    string? BehaviorDescription);

public sealed record DogProfileQualityResult(
    int OverallScore,
    string Summary,
    IReadOnlyList<DogProfileQualityIssue> Issues,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<DogProfileQualitySuggestion> Suggestions,
    DogProfileRewriteSuggestion? SuggestedRewrite,
    IReadOnlyList<string> QuestionsForShelter,
    IReadOnlyList<string> SafetyNotes,
    bool UsedAi,
    string? FallbackReason);

public sealed record OpenAiDogProfileQualityResponse(
    bool Success,
    DogProfileQualityResult? Result,
    string? ErrorMessage)
{
    public static OpenAiDogProfileQualityResponse Failed(string message)
    {
        return new OpenAiDogProfileQualityResponse(false, null, message);
    }

    public static OpenAiDogProfileQualityResponse Successful(DogProfileQualityResult result)
    {
        return new OpenAiDogProfileQualityResponse(true, result, null);
    }
}
