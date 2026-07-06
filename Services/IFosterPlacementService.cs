namespace PawConnect.Services;

public interface IFosterPlacementService
{
    Task<IReadOnlyList<FosterCaregiverProfileDto>> GetCaregiversForShelterAsync(int shelterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FosterCaregiverProfileDto>> GetAvailableCaregiversAsync(int? shelterId = null, CancellationToken cancellationToken = default);

    Task<FosterCaregiverProfileDto> CreateCaregiverAsync(FosterCaregiverCreateRequest request, string currentUserId, int? shelterId = null, CancellationToken cancellationToken = default);

    Task<FosterCaregiverProfileDto> UpdateCaregiverAsync(int caregiverId, FosterCaregiverUpdateRequest request, string currentUserId, int? shelterId = null, bool isAdmin = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FosterPlacementDto>> GetShelterPlacementsAsync(int shelterId, FosterPlacementFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FosterPlacementDto>> GetAdminPlacementsAsync(FosterPlacementFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<FosterPlacementDetailsDto?> GetPlacementDetailsAsync(int placementId, int? shelterId = null, bool isAdmin = false, CancellationToken cancellationToken = default);

    Task<FosterPlacementDto> CreatePlacementAsync(int shelterId, string createdByUserId, FosterPlacementCreateRequest request, CancellationToken cancellationToken = default);

    Task<FosterPlacementDetailsDto> UpdatePlacementAsync(int placementId, int shelterId, string currentUserId, FosterPlacementUpdateRequest request, CancellationToken cancellationToken = default);

    Task<FosterPlacementDetailsDto> ApprovePlacementAsync(int placementId, int shelterId, string approvedByUserId, FosterPlacementDecisionRequest? request = null, CancellationToken cancellationToken = default);

    Task<FosterPlacementDetailsDto> StartPlacementAsync(int placementId, int shelterId, string startedByUserId, FosterPlacementStartRequest? request = null, CancellationToken cancellationToken = default);

    Task<FosterPlacementDetailsDto> CompletePlacementAsync(int placementId, int? shelterId, string completedByUserId, bool isAdmin, FosterPlacementCompleteRequest request, CancellationToken cancellationToken = default);

    Task<FosterPlacementDetailsDto> CancelPlacementAsync(int placementId, int? shelterId, string cancelledByUserId, bool isAdmin = false, FosterPlacementDecisionRequest? request = null, CancellationToken cancellationToken = default);

    Task<FosterPlacementDetailsDto> AddPlacementNoteAsync(int placementId, int shelterId, string currentUserId, FosterPlacementNoteRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DogFosterHistoryItemDto>> GetDogFosterHistoryAsync(int dogId, int? shelterId = null, bool isAdmin = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FosterPlacementDto>> GetCaregiverPlacementsAsync(int caregiverId, int? shelterId = null, bool isAdmin = false, CancellationToken cancellationToken = default);

    Task<FosterPlacementStatsDto> GetFosterStatsAsync(int? shelterId = null, CancellationToken cancellationToken = default);
}
