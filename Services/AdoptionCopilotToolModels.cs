using PawConnect.Entities;

namespace PawConnect.Services;

public sealed class AdoptionCopilotSearchDogsArgs
{
    public string? Query { get; set; }

    public string? PrimaryIntent { get; set; }

    public List<string>? Sizes { get; set; }

    public List<string>? Breeds { get; set; }

    public List<string>? CoatColors { get; set; }

    public string? City { get; set; }

    public string? Neighborhood { get; set; }

    public string? ShelterName { get; set; }

    public int? MaxAgeYears { get; set; }

    public int? MinAgeYears { get; set; }

    public string? AgeComparison { get; set; }

    public List<string>? Statuses { get; set; }

    public List<string>? BehaviorTerms { get; set; }

    public List<string>? TemperamentTags { get; set; }

    public List<string>? Temperaments { get; set; }

    public string? EnergyLevel { get; set; }

    public string? ActivityLevel { get; set; }

    public string? HomeType { get; set; }

    public string? HousingPreference { get; set; }

    public bool? ApartmentFriendly { get; set; }

    public bool? YardFriendly { get; set; }

    public bool? YardRequired { get; set; }

    public bool? NeedsYard { get; set; }

    public bool? GoodWithChildren { get; set; }

    public bool? GoodWithPets { get; set; }

    public List<string>? Compatibility { get; set; }

    public string? CompatibilityTarget { get; set; }

    public string? ExperienceLevel { get; set; }

    public List<string>? DesiredTraits { get; set; }

    public List<string>? MustHave { get; set; }

    public List<string>? NiceToHave { get; set; }

    public List<string>? AvoidTraits { get; set; }

    public List<string>? Avoid { get; set; }

    public List<string>? EvidenceToLookFor { get; set; }

    public List<string>? DisplayChipIntent { get; set; }

    public string? NearLocationText { get; set; }

    public int? RadiusKm { get; set; }

    public string? Sort { get; set; }

    public int? Limit { get; set; }

    public int Count { get; set; } = 8;
}

public sealed record CopilotIntent(
    string PrimaryIntent,
    string CompatibilityTarget,
    string HomeType,
    string ActivityLevel,
    string RealLifeNeed,
    IReadOnlyList<string> MustHaveEvidence,
    IReadOnlyList<string> NiceToHaveEvidence,
    IReadOnlyList<string> NegativeEvidence,
    IReadOnlyList<string> SecondarySignals,
    IReadOnlyList<string> Chips,
    IReadOnlyList<string> Statuses,
    string? City,
    string? Neighborhood,
    IReadOnlyList<string> Sizes,
    int Limit);

public sealed record EvidenceItem(
    string Label,
    string Strength,
    string SourceField,
    string? MatchedText);

public sealed record CopilotDogEvidence(
    int DogId,
    IReadOnlyList<string> DirectEvidence,
    IReadOnlyList<string> IndirectEvidence,
    IReadOnlyList<string> GenericEvidence,
    IReadOnlyList<string> PositiveEvidence,
    IReadOnlyList<string> CautionEvidence,
    IReadOnlyList<string> NegativeEvidence,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<EvidenceItem> PositiveEvidenceItems,
    IReadOnlyList<EvidenceItem> CautionEvidenceItems,
    IReadOnlyList<EvidenceItem> NegativeEvidenceItems,
    IReadOnlyList<EvidenceItem> MissingEvidenceItems,
    IReadOnlyList<string> SupportedDisplayTags,
    string EvidenceSummary);

public sealed record AdoptionCopilotToolSearchResult(
    IReadOnlyList<AdoptionCopilotToolDogCandidate> Dogs,
    IReadOnlyList<AdoptionCopilotConstraint> AppliedConstraints,
    bool UsedSemanticSearch,
    string? EmptyReason = null);

public sealed record AdoptionCopilotToolDogCandidate(
    int DogId,
    Dog Dog,
    int ScorePercent,
    string MatchLabel,
    IReadOnlyList<string> SafeReasons,
    string SuggestedNextAction,
    double? DistanceKm = null,
    IReadOnlyList<string>? DisplayTags = null,
    IReadOnlyList<string>? CautionTags = null,
    string? EvidenceSummary = null,
    IReadOnlyList<EvidenceItem>? PositiveEvidence = null,
    IReadOnlyList<EvidenceItem>? CautionEvidence = null,
    IReadOnlyList<EvidenceItem>? NegativeEvidence = null,
    IReadOnlyList<EvidenceItem>? MissingEvidence = null);

public sealed record AdoptionCopilotDogToolDto(
    int DogId,
    string Name,
    string Breed,
    string? CoatColor,
    string AgeText,
    string Size,
    string Status,
    string CatCompatibility,
    string DogCompatibility,
    string ChildrenCompatibility,
    string ActivityLevel,
    string ExperienceNeeded,
    string ApartmentSuitability,
    string? PublicDescription,
    string? BehaviorDescription,
    string? ShelterName,
    string? ShelterCity,
    string? ShelterNeighborhood,
    double? DistanceKm,
    string? MainImageUrl,
    IReadOnlyList<string> SafeReasons,
    IReadOnlyList<string> DisplayTags,
    IReadOnlyList<string> CautionTags,
    IReadOnlyList<EvidenceItem> PositiveEvidence,
    IReadOnlyList<EvidenceItem> CautionEvidence,
    IReadOnlyList<EvidenceItem> NegativeEvidence,
    IReadOnlyList<EvidenceItem> MissingEvidence,
    string? EvidenceSummary,
    int ScorePercent,
    string MatchLabel);

public sealed record AdoptionCopilotToolJsonResult(
    bool Success,
    string? Message,
    IReadOnlyList<AdoptionCopilotDogToolDto> Dogs,
    IReadOnlyList<AdoptionCopilotConstraint> AppliedConstraints);

public sealed record AdoptionCopilotProfileToolResult(
    string? City,
    string? HousingType,
    bool? HasYard,
    bool? HasPets,
    bool? HasChildren,
    string? ExperienceWithDogs);

public sealed record AdoptionCopilotPreferenceToolResult(
    IReadOnlyList<string> CommonSizes,
    IReadOnlyList<string> CommonBreeds,
    IReadOnlyList<string> CommonShelterCities);
