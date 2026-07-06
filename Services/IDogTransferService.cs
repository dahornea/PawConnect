namespace PawConnect.Services;

public interface IDogTransferService
{
    Task<IReadOnlyList<DogTransferRequestDto>> GetIncomingTransfersAsync(int shelterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DogTransferRequestDto>> GetOutgoingTransfersAsync(int shelterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DogTransferRequestDto>> GetAdminTransfersAsync(DogTransferFilterDto? filter = null, CancellationToken cancellationToken = default);

    Task<DogTransferDetailsDto?> GetTransferDetailsAsync(int transferId, int? shelterId = null, bool isAdmin = false, CancellationToken cancellationToken = default);

    Task<DogTransferRequestDto> CreateTransferRequestAsync(int sourceShelterId, string requestedByUserId, DogTransferCreateRequest request, CancellationToken cancellationToken = default);

    Task<DogTransferDetailsDto> ApproveTransferAsync(int transferId, int destinationShelterId, string respondedByUserId, DogTransferDecisionRequest request, CancellationToken cancellationToken = default);

    Task<DogTransferDetailsDto> RejectTransferAsync(int transferId, int destinationShelterId, string respondedByUserId, DogTransferDecisionRequest request, CancellationToken cancellationToken = default);

    Task<DogTransferDetailsDto> CancelTransferAsync(int transferId, int? sourceShelterId, string cancelledByUserId, bool isAdmin = false, DogTransferDecisionRequest? request = null, CancellationToken cancellationToken = default);

    Task<DogTransferDetailsDto> CompleteTransferAsync(int transferId, int? shelterId, string completedByUserId, bool isAdmin = false, DogTransferCompleteRequest? request = null, CancellationToken cancellationToken = default);

    Task<DogTransferDetailsDto> UpdateAdminNotesAsync(int transferId, string? adminNotes, string adminUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DogTransferHistoryItemDto>> GetDogTransferHistoryAsync(int dogId, int? shelterId = null, bool isAdmin = false, CancellationToken cancellationToken = default);

    Task<DogTransferStatsDto> GetTransferStatsAsync(int? shelterId = null, CancellationToken cancellationToken = default);
}
