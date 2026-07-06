namespace PawConnect.Services;

public interface IVolunteerTaskService
{
    Task<VolunteerProfileDto?> GetVolunteerProfileForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerProfileDto>> GetVolunteersForShelterAsync(int shelterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerProfileDto>> GetAllVolunteersAsync(CancellationToken cancellationToken = default);

    Task<VolunteerProfileDto> CreateVolunteerProfileAsync(VolunteerProfileCreateRequest request, CancellationToken cancellationToken = default);

    Task<VolunteerProfileDto> UpdateVolunteerProfileAsync(int volunteerProfileId, VolunteerProfileUpdateRequest request, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDto> CreateTaskAsync(int shelterId, string createdByUserId, VolunteerTaskCreateRequest request, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto> UpdateTaskAsync(int taskId, int shelterId, string updatedByUserId, VolunteerTaskUpdateRequest request, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto> AssignTaskAsync(int taskId, int shelterId, string assignedByUserId, VolunteerTaskAssignRequest request, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto> AcceptTaskAsync(int taskId, string volunteerUserId, VolunteerTaskActionRequest? request = null, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto> StartTaskAsync(int taskId, string volunteerUserId, VolunteerTaskActionRequest? request = null, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto> CompleteTaskAsync(int taskId, string volunteerUserId, VolunteerTaskActionRequest? request = null, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto> CancelTaskAsync(int taskId, int? shelterId, string cancelledByUserId, bool isAdmin = false, VolunteerTaskActionRequest? request = null, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto> ReopenTaskAsync(int taskId, int? shelterId, string reopenedByUserId, bool isAdmin = false, VolunteerTaskActionRequest? request = null, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto> AddTaskCommentAsync(int taskId, string userId, VolunteerTaskActionRequest request, int? shelterId = null, bool isAdmin = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerTaskDto>> GetShelterTasksAsync(int shelterId, VolunteerTaskFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerTaskDto>> GetVolunteerTasksAsync(string volunteerUserId, VolunteerTaskFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerTaskDto>> GetOpenTasksForVolunteerAsync(string volunteerUserId, VolunteerTaskFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerTaskDto>> GetAdminTasksAsync(VolunteerTaskFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<VolunteerTaskDetailsDto?> GetTaskDetailsAsync(int taskId, int? shelterId = null, string? volunteerUserId = null, bool isAdmin = false, CancellationToken cancellationToken = default);

    Task<VolunteerTaskStatsDto> GetTaskStatsAsync(int? shelterId = null, string? volunteerUserId = null, CancellationToken cancellationToken = default);
}
