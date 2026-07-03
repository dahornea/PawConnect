using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record ShelterAdoptionPipelineDto(
    int ShelterId,
    string ShelterName,
    int TotalRequests,
    IReadOnlyList<AdoptionPipelineColumnDto> Columns);

public sealed record AdoptionPipelineColumnDto(
    AdoptionPipelineStage Stage,
    string Title,
    string Description,
    IReadOnlyList<AdoptionPipelineCardDto> Cards);

public sealed record AdoptionPipelineCardDto(
    int AdoptionRequestId,
    int DogId,
    string DogName,
    string Breed,
    string? DogImageUrl,
    DogStatus DogStatus,
    string AdopterDisplayName,
    string? AdopterCity,
    AdoptionRequestStatus RequestStatus,
    AdoptionVisitStatus VisitStatus,
    DateTime? PreferredVisitDateTime,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? QuestionnairePreview,
    bool CanConfirmVisit,
    bool CanReject,
    bool CanAccept,
    bool CanCancel);

public enum AdoptionPipelineStage
{
    Pending,
    VisitConfirmed,
    Accepted,
    Closed
}
