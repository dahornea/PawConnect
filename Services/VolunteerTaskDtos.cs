using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record VolunteerProfileCreateRequest(
    string UserId,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    int? PreferredShelterId,
    string? Skills,
    string? AvailabilityNotes,
    bool IsActive = true);

public sealed record VolunteerProfileUpdateRequest(
    string DisplayName,
    string Email,
    string? PhoneNumber,
    int? PreferredShelterId,
    string? Skills,
    string? AvailabilityNotes,
    bool IsActive);

public sealed record VolunteerTaskCreateRequest(
    string Title,
    string? Description,
    VolunteerTaskCategory Category,
    VolunteerTaskPriority Priority,
    DateTime ScheduledStartUtc,
    DateTime ScheduledEndUtc,
    DateTime? DueAtUtc = null,
    int? DogId = null,
    int? AssignedVolunteerProfileId = null,
    string? Location = null,
    string? RequiredSkills = null,
    string? ShelterNotes = null);

public sealed record VolunteerTaskUpdateRequest(
    string Title,
    string? Description,
    VolunteerTaskCategory Category,
    VolunteerTaskPriority Priority,
    DateTime ScheduledStartUtc,
    DateTime ScheduledEndUtc,
    DateTime? DueAtUtc = null,
    int? DogId = null,
    string? Location = null,
    string? RequiredSkills = null,
    string? ShelterNotes = null);

public sealed record VolunteerTaskAssignRequest(int? VolunteerProfileId, string? Notes = null);

public sealed record VolunteerTaskActionRequest(string? Notes = null);

public sealed record VolunteerTaskFilterDto
{
    public VolunteerTaskFilterDto()
    {
    }

    public VolunteerTaskFilterDto(
        VolunteerTaskStatus? status = null,
        VolunteerTaskCategory? category = null,
        VolunteerTaskPriority? priority = null,
        int? shelterId = null,
        int? assignedVolunteerProfileId = null,
        string? search = null)
    {
        Status = status;
        Category = category;
        Priority = priority;
        ShelterId = shelterId;
        AssignedVolunteerProfileId = assignedVolunteerProfileId;
        Search = search;
    }

    public VolunteerTaskStatus? Status { get; init; }
    public VolunteerTaskCategory? Category { get; init; }
    public VolunteerTaskPriority? Priority { get; init; }
    public int? ShelterId { get; init; }
    public int? AssignedVolunteerProfileId { get; init; }
    public string? Search { get; init; }
}

public sealed record VolunteerTaskStatsDto(
    int OpenTasks,
    int AssignedTasks,
    int InProgressTasks,
    int CompletedThisWeek,
    int OverdueTasks,
    int ActiveVolunteers,
    int TotalTasks);

public sealed record VolunteerProfileDto(
    int Id,
    string UserId,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    int? PreferredShelterId,
    string? PreferredShelterName,
    string? Skills,
    string? AvailabilityNotes,
    bool IsActive);

public sealed record VolunteerTaskActivityDto(
    int Id,
    VolunteerTaskActivityType ActivityType,
    string? Message,
    string? ActorDisplayName,
    DateTime CreatedAtUtc);

public sealed record VolunteerTaskDto(
    int Id,
    int ShelterId,
    string ShelterName,
    int? DogId,
    string? DogName,
    string Title,
    string? Description,
    VolunteerTaskCategory Category,
    VolunteerTaskStatus Status,
    VolunteerTaskPriority Priority,
    DateTime ScheduledStartUtc,
    DateTime ScheduledEndUtc,
    DateTime? DueAtUtc,
    string? Location,
    string? RequiredSkills,
    int? AssignedVolunteerProfileId,
    string? AssignedVolunteerName,
    DateTime? AssignedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    bool CanAssign,
    bool CanAccept,
    bool CanStart,
    bool CanComplete,
    bool CanCancel);

public sealed record VolunteerTaskDetailsDto(
    int Id,
    int ShelterId,
    string ShelterName,
    int? DogId,
    string? DogName,
    string? DogBreed,
    string Title,
    string? Description,
    VolunteerTaskCategory Category,
    VolunteerTaskStatus Status,
    VolunteerTaskPriority Priority,
    DateTime ScheduledStartUtc,
    DateTime ScheduledEndUtc,
    DateTime? DueAtUtc,
    string? Location,
    string? RequiredSkills,
    string? ShelterNotes,
    string? VolunteerNotes,
    string? CompletionNotes,
    int? AssignedVolunteerProfileId,
    string? AssignedVolunteerName,
    string CreatedByDisplayName,
    DateTime? AssignedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    bool CanAssign,
    bool CanAccept,
    bool CanStart,
    bool CanComplete,
    bool CanCancel,
    IReadOnlyList<VolunteerTaskActivityDto> Activities);
