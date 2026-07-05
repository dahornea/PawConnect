namespace PawConnect.DTOs.Api;

public sealed record ShelterListItemApiDto(
    int Id,
    string Name,
    string? Description,
    string Address,
    string City,
    string? Neighborhood,
    string? PhoneNumber,
    string? Email,
    double? Latitude,
    double? Longitude,
    int PublicDogCount);

public sealed record ShelterDetailsApiDto(
    int Id,
    string Name,
    string? Description,
    string Address,
    string City,
    string? Neighborhood,
    string? PhoneNumber,
    string? Email,
    double? Latitude,
    double? Longitude,
    ShelterVisitScheduleApiDto VisitSchedule,
    IReadOnlyList<DogListItemApiDto> Dogs);

public sealed record ShelterSummaryApiDto(
    int Id,
    string Name,
    string City,
    string? Neighborhood,
    string? Email,
    string? PhoneNumber,
    double? Latitude,
    double? Longitude);

public sealed record ShelterVisitScheduleApiDto(
    string? StartTime,
    string? EndTime,
    IReadOnlyList<string> VisitDays);
