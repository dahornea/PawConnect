using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record FosterCaregiverProfileDto(
    int Id,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    string? AddressSummary,
    int? PreferredShelterId,
    string? PreferredShelterName,
    string? ExperienceNotes,
    string? HomeEnvironmentNotes,
    int Capacity,
    int ActivePlacementCount,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record FosterCaregiverCreateRequest(
    string DisplayName,
    string Email,
    string? PhoneNumber,
    string? AddressSummary,
    int? PreferredShelterId,
    string? ExperienceNotes,
    string? HomeEnvironmentNotes,
    int Capacity = 1,
    bool IsActive = true);

public sealed record FosterCaregiverUpdateRequest(
    string DisplayName,
    string Email,
    string? PhoneNumber,
    string? AddressSummary,
    int? PreferredShelterId,
    string? ExperienceNotes,
    string? HomeEnvironmentNotes,
    int Capacity,
    bool IsActive);

public sealed record FosterPlacementCreateRequest(
    int DogId,
    int FosterCaregiverProfileId,
    FosterPlacementPriority Priority,
    FosterPlacementReason Reason,
    DateTime StartDateUtc,
    DateTime? PlannedEndDateUtc,
    string? CareInstructions,
    string? MedicalNotesSummary,
    string? ShelterNotes);

public sealed record FosterPlacementUpdateRequest(
    FosterPlacementPriority Priority,
    FosterPlacementReason Reason,
    DateTime StartDateUtc,
    DateTime? PlannedEndDateUtc,
    string? CareInstructions,
    string? MedicalNotesSummary,
    string? ShelterNotes);

public sealed record FosterPlacementDecisionRequest(string? Notes = null);

public sealed record FosterPlacementStartRequest(string? Notes = null);

public sealed record FosterPlacementCompleteRequest(
    DateTime ActualEndDateUtc,
    string? CompletionNotes,
    string? FosterNotes = null);

public sealed record FosterPlacementNoteRequest(string? Notes = null);

public sealed record FosterPlacementFilterDto
{
    public FosterPlacementFilterDto()
    {
    }

    public FosterPlacementFilterDto(
        FosterPlacementStatus? status = null,
        FosterPlacementPriority? priority = null,
        FosterPlacementReason? reason = null,
        int? shelterId = null,
        int? caregiverId = null,
        int? dogId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? search = null)
    {
        Status = status;
        Priority = priority;
        Reason = reason;
        ShelterId = shelterId;
        CaregiverId = caregiverId;
        DogId = dogId;
        From = from;
        To = to;
        Search = search;
    }

    public FosterPlacementStatus? Status { get; init; }
    public FosterPlacementPriority? Priority { get; init; }
    public FosterPlacementReason? Reason { get; init; }
    public int? ShelterId { get; init; }
    public int? CaregiverId { get; init; }
    public int? DogId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Search { get; init; }
}

public sealed record FosterPlacementStatsDto(
    int Pending,
    int Approved,
    int Active,
    int EndingSoon,
    int Overdue,
    int CompletedThisMonth,
    int AvailableCaregivers,
    int Total);

public sealed record FosterPlacementDto(
    int Id,
    int DogId,
    string DogName,
    string DogBreed,
    int ShelterId,
    string ShelterName,
    int FosterCaregiverProfileId,
    string CaregiverName,
    FosterPlacementStatus Status,
    FosterPlacementPriority Priority,
    FosterPlacementReason Reason,
    DateTime StartDateUtc,
    DateTime? PlannedEndDateUtc,
    DateTime? ActualEndDateUtc,
    int DaysInFosterCare,
    bool IsEndingSoon,
    bool IsOverdue,
    bool CanApprove,
    bool CanStart,
    bool CanComplete,
    bool CanCancel);

public sealed record FosterPlacementDetailsDto(
    int Id,
    int DogId,
    string DogName,
    string DogBreed,
    int ShelterId,
    string ShelterName,
    int FosterCaregiverProfileId,
    string CaregiverName,
    string CaregiverEmail,
    string? CaregiverPhoneNumber,
    FosterPlacementStatus Status,
    FosterPlacementPriority Priority,
    FosterPlacementReason Reason,
    DateTime StartDateUtc,
    DateTime? PlannedEndDateUtc,
    DateTime? ActualEndDateUtc,
    string? CareInstructions,
    string? MedicalNotesSummary,
    string? ShelterNotes,
    string? FosterNotes,
    string? CompletionNotes,
    string CreatedByDisplayName,
    string? ApprovedByDisplayName,
    string? EndedByDisplayName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<FosterPlacementActivityDto> Activities,
    bool CanApprove,
    bool CanStart,
    bool CanComplete,
    bool CanCancel);

public sealed record FosterPlacementActivityDto(
    int Id,
    FosterPlacementActivityType ActivityType,
    string Message,
    string? ActorDisplayName,
    DateTime CreatedAtUtc);

public sealed record DogFosterHistoryItemDto(
    int Id,
    FosterPlacementStatus Status,
    FosterPlacementPriority Priority,
    FosterPlacementReason Reason,
    string ShelterName,
    string CaregiverName,
    DateTime StartDateUtc,
    DateTime? PlannedEndDateUtc,
    DateTime? ActualEndDateUtc,
    string? Summary);
