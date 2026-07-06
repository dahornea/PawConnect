using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record SavedDogSearchCriteriaDto(
    string? SearchText = null,
    int? ShelterId = null,
    string? Breed = null,
    string? CoatColor = null,
    int? MaxAgeYears = null,
    DogSize? Size = null,
    string? Location = null,
    string? Neighborhood = null,
    DogStatus? Status = null,
    CatCompatibility? CatCompatibility = null,
    ChildrenCompatibility? ChildrenCompatibility = null,
    DogActivityLevel? ActivityLevel = null,
    ApartmentSuitability? ApartmentSuitability = null,
    DogSortOption SortOption = DogSortOption.NameAsc,
    string? NearbyLabel = null,
    double? NearbyLatitude = null,
    double? NearbyLongitude = null,
    int? RadiusKm = null);

public sealed record SavedDogSearchCreateRequest(
    string Name,
    SavedDogSearchCriteriaDto Criteria,
    bool AlertsEnabled = true,
    SavedSearchAlertFrequency AlertFrequency = SavedSearchAlertFrequency.Immediate);

public sealed record SavedDogSearchUpdateRequest(
    string Name,
    SavedDogSearchCriteriaDto Criteria,
    bool AlertsEnabled,
    SavedSearchAlertFrequency AlertFrequency);

public sealed record SavedDogSearchDto(
    int Id,
    string Name,
    bool AlertsEnabled,
    SavedSearchAlertFrequency AlertFrequency,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastEvaluatedAtUtc,
    DateTime? LastMatchAtUtc,
    IReadOnlyList<string> CriteriaLabels,
    int TotalMatches,
    int NewMatches);

public sealed record SavedSearchMatchDto(
    int Id,
    int DogId,
    string DogName,
    string BreedText,
    string AgeText,
    DogSize Size,
    string Location,
    DogStatus Status,
    string? ShelterName,
    string? ShelterNeighborhood,
    string? MainImageUrl,
    int MatchScore,
    SavedSearchMatchStatus StatusInSearch,
    IReadOnlyList<string> MatchReasons,
    DateTime FirstMatchedAtUtc,
    DateTime LastMatchedAtUtc);

public sealed record SavedDogSearchDetailsDto(
    SavedDogSearchDto Search,
    IReadOnlyList<SavedSearchMatchDto> Matches);

public sealed record SavedSearchStatsDto(
    int SavedSearches,
    int SearchesWithAlerts,
    int NewMatches,
    DateTime? LastMatchAtUtc);
