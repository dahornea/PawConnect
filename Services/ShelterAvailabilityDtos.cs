namespace PawConnect.Services;

public sealed record ShelterAvailabilitySlotDto(
    int Id,
    int ShelterId,
    DateTime StartTime,
    DateTime EndTime,
    bool IsBooked,
    int? BookedAdoptionRequestId,
    string? BookedDogName,
    string? BookedAdopterName,
    bool IsCancelled,
    bool IsPast,
    string? Notes);

public sealed record CreateShelterAvailabilitySlotRequest(
    int ShelterId,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes = null);
