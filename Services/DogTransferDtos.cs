using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record DogTransferCreateRequest(
    int DogId,
    int DestinationShelterId,
    DogTransferPriority Priority,
    string Reason,
    string? SourceShelterNotes = null);

public sealed record DogTransferDecisionRequest(string? Notes = null);

public sealed record DogTransferCompleteRequest(string? Notes = null);

public sealed record DogTransferAdminNoteRequest(string? AdminNotes = null);

public sealed record DogTransferFilterDto
{
    public DogTransferFilterDto()
    {
    }

    public DogTransferFilterDto(
        DogTransferStatus? status = null,
        DogTransferPriority? priority = null,
        int? sourceShelterId = null,
        int? destinationShelterId = null,
        string? search = null)
    {
        Status = status;
        Priority = priority;
        SourceShelterId = sourceShelterId;
        DestinationShelterId = destinationShelterId;
        Search = search;
    }

    public DogTransferStatus? Status { get; init; }
    public DogTransferPriority? Priority { get; init; }
    public int? SourceShelterId { get; init; }
    public int? DestinationShelterId { get; init; }
    public string? Search { get; init; }
}

public sealed record DogTransferStatsDto(
    int IncomingPending,
    int OutgoingPending,
    int ApprovedWaitingCompletion,
    int Completed,
    int UrgentRequests,
    int Total);

public sealed record DogTransferRequestDto(
    int Id,
    int DogId,
    string DogName,
    string DogBreed,
    DogTransferStatus Status,
    DogTransferPriority Priority,
    int SourceShelterId,
    string SourceShelterName,
    int DestinationShelterId,
    string DestinationShelterName,
    string ReasonPreview,
    DateTime RequestedAtUtc,
    DateTime? RespondedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    bool CanApprove,
    bool CanReject,
    bool CanCancel,
    bool CanComplete);

public sealed record DogTransferDetailsDto(
    int Id,
    int DogId,
    string DogName,
    string DogBreed,
    DogTransferStatus Status,
    DogTransferPriority Priority,
    int SourceShelterId,
    string SourceShelterName,
    int DestinationShelterId,
    string DestinationShelterName,
    string Reason,
    string? SourceShelterNotes,
    string? DestinationShelterResponseNotes,
    string? AdminNotes,
    string RequestedByDisplayName,
    string? RespondedByDisplayName,
    string? CompletedByDisplayName,
    DateTime RequestedAtUtc,
    DateTime? RespondedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime UpdatedAtUtc,
    bool CanApprove,
    bool CanReject,
    bool CanCancel,
    bool CanComplete);

public sealed record DogTransferHistoryItemDto(
    int Id,
    int DogId,
    string DogName,
    DogTransferStatus Status,
    DogTransferPriority Priority,
    string SourceShelterName,
    string DestinationShelterName,
    string ReasonPreview,
    DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? RespondedAtUtc,
    DateTime? CancelledAtUtc);
