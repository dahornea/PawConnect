using System.ComponentModel.DataAnnotations;
using PawConnect.Entities;

namespace PawConnect.DTOs.Api;

public sealed record CreateAdoptionApplicationApiRequest(
    [property: Required]
    [property: StringLength(1000, MinimumLength = 10)]
    string ReasonForAdoption,
    [property: Range(0, 24)]
    int? HoursAlonePerDay,
    DateTime? PreferredVisitDateTime,
    [property: StringLength(1000)]
    string? AdditionalInformation);

public sealed record AdoptionApplicationApiDto(
    int Id,
    int DogId,
    string DogName,
    string DogBreed,
    int ShelterId,
    string ShelterName,
    AdoptionRequestStatus Status,
    AdoptionVisitStatus VisitStatus,
    DateTime? PreferredVisitDateTime,
    DateTime? VisitConfirmedAt,
    string ReasonForAdoption,
    int? HoursAlonePerDay,
    string? AdditionalInformation,
    DateTime CreatedAt,
    DateTime UpdatedAt);
