using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogTransferService(
    ApplicationDbContext context,
    INotificationService? notificationService = null,
    IAuditLogService? auditLogService = null,
    IDogSearchEmbeddingService? dogSearchEmbeddingService = null,
    ILogger<DogTransferService>? logger = null) : IDogTransferService
{
    private static readonly DogTransferStatus[] ActiveStatuses = [DogTransferStatus.Pending, DogTransferStatus.Approved];
    private const int ReasonMaxLength = 1000;
    private const int NotesMaxLength = 1000;

    public async Task<IReadOnlyList<DogTransferRequestDto>> GetIncomingTransfersAsync(
        int shelterId,
        CancellationToken cancellationToken = default)
    {
        var transfers = await BaseQuery()
            .Where(transfer => transfer.DestinationShelterId == shelterId)
            .OrderByDescending(transfer => transfer.Status == DogTransferStatus.Pending)
            .ThenByDescending(transfer => transfer.Priority)
            .ThenByDescending(transfer => transfer.RequestedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return transfers.Select(transfer => ToListDto(transfer, shelterId, isAdmin: false)).ToList();
    }

    public async Task<IReadOnlyList<DogTransferRequestDto>> GetOutgoingTransfersAsync(
        int shelterId,
        CancellationToken cancellationToken = default)
    {
        var transfers = await BaseQuery()
            .Where(transfer => transfer.SourceShelterId == shelterId)
            .OrderByDescending(transfer => transfer.Status == DogTransferStatus.Pending || transfer.Status == DogTransferStatus.Approved)
            .ThenByDescending(transfer => transfer.Priority)
            .ThenByDescending(transfer => transfer.RequestedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return transfers.Select(transfer => ToListDto(transfer, shelterId, isAdmin: false)).ToList();
    }

    public async Task<IReadOnlyList<DogTransferRequestDto>> GetAdminTransfersAsync(
        DogTransferFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery();
        filter ??= new DogTransferFilterDto();

        if (filter.Status.HasValue)
        {
            query = query.Where(transfer => transfer.Status == filter.Status.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(transfer => transfer.Priority == filter.Priority.Value);
        }

        if (filter.SourceShelterId.HasValue)
        {
            query = query.Where(transfer => transfer.SourceShelterId == filter.SourceShelterId.Value);
        }

        if (filter.DestinationShelterId.HasValue)
        {
            query = query.Where(transfer => transfer.DestinationShelterId == filter.DestinationShelterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var normalizedSearch = filter.Search.Trim().ToUpper();
            query = query.Where(transfer =>
                transfer.Dog != null && transfer.Dog.Name.ToUpper().Contains(normalizedSearch) ||
                transfer.SourceShelter != null && transfer.SourceShelter.Name.ToUpper().Contains(normalizedSearch) ||
                transfer.DestinationShelter != null && transfer.DestinationShelter.Name.ToUpper().Contains(normalizedSearch) ||
                transfer.Reason.ToUpper().Contains(normalizedSearch));
        }

        var transfers = await query
            .OrderByDescending(transfer => transfer.Status == DogTransferStatus.Pending || transfer.Status == DogTransferStatus.Approved)
            .ThenByDescending(transfer => transfer.Priority)
            .ThenByDescending(transfer => transfer.RequestedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return transfers.Select(transfer => ToListDto(transfer, viewerShelterId: null, isAdmin: true)).ToList();
    }

    public async Task<DogTransferDetailsDto?> GetTransferDetailsAsync(
        int transferId,
        int? shelterId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var transfer = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(transfer => transfer.Id == transferId, cancellationToken);

        return transfer is null || !CanView(transfer, shelterId, isAdmin)
            ? null
            : ToDetailsDto(transfer, shelterId, isAdmin);
    }

    public async Task<DogTransferRequestDto> CreateTransferRequestAsync(
        int sourceShelterId,
        string requestedByUserId,
        DogTransferCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(requestedByUserId);
        var reason = NormalizeRequired(request.Reason, "Transfer reason is required.", ReasonMaxLength);
        var sourceNotes = NormalizeOptional(request.SourceShelterNotes, NotesMaxLength);

        if (request.DestinationShelterId <= 0)
        {
            throw new InvalidOperationException("Destination shelter is required.");
        }

        if (request.DestinationShelterId == sourceShelterId)
        {
            throw new InvalidOperationException("Source and destination shelters cannot be the same.");
        }

        var dog = await context.Dogs
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .FirstOrDefaultAsync(dog => dog.Id == request.DogId, cancellationToken);

        if (dog is null || dog.ShelterId != sourceShelterId)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        var destinationShelterExists = await context.Shelters
            .AnyAsync(shelter => shelter.Id == request.DestinationShelterId, cancellationToken);
        if (!destinationShelterExists)
        {
            throw new InvalidOperationException("Destination shelter was not found.");
        }

        var activeRequestExists = await context.DogTransferRequests.AnyAsync(
            transfer => transfer.DogId == dog.Id && ActiveStatuses.Contains(transfer.Status),
            cancellationToken);
        if (activeRequestExists)
        {
            throw new InvalidOperationException("This dog already has an active transfer request.");
        }

        var now = DateTime.UtcNow;
        var transferRequest = new DogTransferRequest
        {
            DogId = dog.Id,
            SourceShelterId = sourceShelterId,
            DestinationShelterId = request.DestinationShelterId,
            RequestedByUserId = requestedByUserId,
            Status = DogTransferStatus.Pending,
            Priority = request.Priority,
            Reason = reason,
            SourceShelterNotes = sourceNotes,
            RequestedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        context.DogTransferRequests.Add(transferRequest);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTransferAsync(transferRequest.Id, cancellationToken);
        await NotifyTransferRequestedAsync(saved);
        await LogAsync(AuditActions.DogTransferRequested, saved, requestedByUserId, $"Transfer requested for dog {saved.Dog?.Name}.");

        return ToListDto(saved, sourceShelterId, isAdmin: false);
    }

    public Task<DogTransferDetailsDto> ApproveTransferAsync(
        int transferId,
        int destinationShelterId,
        string respondedByUserId,
        DogTransferDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        return RespondAsync(transferId, destinationShelterId, respondedByUserId, DogTransferStatus.Approved, request, cancellationToken);
    }

    public Task<DogTransferDetailsDto> RejectTransferAsync(
        int transferId,
        int destinationShelterId,
        string respondedByUserId,
        DogTransferDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        return RespondAsync(transferId, destinationShelterId, respondedByUserId, DogTransferStatus.Rejected, request, cancellationToken);
    }

    public async Task<DogTransferDetailsDto> CancelTransferAsync(
        int transferId,
        int? sourceShelterId,
        string cancelledByUserId,
        bool isAdmin = false,
        DogTransferDecisionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(cancelledByUserId);
        var transfer = await LoadTransferForUpdateAsync(transferId, cancellationToken);
        if (!isAdmin && transfer.SourceShelterId != sourceShelterId)
        {
            throw new InvalidOperationException("Only the source shelter can cancel this transfer request.");
        }

        if (transfer.Status != DogTransferStatus.Pending)
        {
            throw new InvalidOperationException("Only pending transfer requests can be cancelled.");
        }

        transfer.Status = DogTransferStatus.Cancelled;
        transfer.CancelledAtUtc = DateTime.UtcNow;
        transfer.UpdatedAtUtc = transfer.CancelledAtUtc.Value;
        if (!string.IsNullOrWhiteSpace(request?.Notes))
        {
            transfer.SourceShelterNotes = MergeNotes(transfer.SourceShelterNotes, "Cancellation note", request.Notes);
        }

        await context.SaveChangesAsync(cancellationToken);
        var saved = await LoadRequiredTransferAsync(transfer.Id, cancellationToken);
        await NotifyTransferCancelledAsync(saved);
        await LogAsync(AuditActions.DogTransferCancelled, saved, cancelledByUserId, $"Transfer request for dog {saved.Dog?.Name} was cancelled.");
        return ToDetailsDto(saved, sourceShelterId, isAdmin);
    }

    public async Task<DogTransferDetailsDto> CompleteTransferAsync(
        int transferId,
        int? shelterId,
        string completedByUserId,
        bool isAdmin = false,
        DogTransferCompleteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(completedByUserId);
        var transfer = await LoadTransferForUpdateAsync(transferId, cancellationToken);
        if (!isAdmin && shelterId != transfer.SourceShelterId && shelterId != transfer.DestinationShelterId)
        {
            throw new InvalidOperationException("Only the source or destination shelter can complete this transfer.");
        }

        if (transfer.Status != DogTransferStatus.Approved)
        {
            throw new InvalidOperationException("Only approved transfer requests can be completed.");
        }

        if (transfer.Dog is null || transfer.Dog.ShelterId != transfer.SourceShelterId)
        {
            throw new InvalidOperationException("The dog is no longer assigned to the source shelter.");
        }

        var now = DateTime.UtcNow;
        transfer.Status = DogTransferStatus.Completed;
        transfer.CompletedByUserId = completedByUserId;
        transfer.CompletedAtUtc = now;
        transfer.UpdatedAtUtc = now;
        if (!string.IsNullOrWhiteSpace(request?.Notes))
        {
            transfer.DestinationShelterResponseNotes = MergeNotes(transfer.DestinationShelterResponseNotes, "Completion note", request.Notes);
        }

        transfer.Dog.ShelterId = transfer.DestinationShelterId;
        transfer.Dog.Shelter = null;

        await context.SaveChangesAsync(cancellationToken);
        var saved = await LoadRequiredTransferAsync(transfer.Id, cancellationToken);
        await NotifyTransferCompletedAsync(saved);
        await LogAsync(AuditActions.DogTransferCompleted, saved, completedByUserId, $"Transfer completed for dog {saved.Dog?.Name}.");
        await RefreshDogSearchEmbeddingBestEffortAsync(saved.DogId);
        return ToDetailsDto(saved, shelterId, isAdmin);
    }

    public async Task<DogTransferDetailsDto> UpdateAdminNotesAsync(
        int transferId,
        string? adminNotes,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(adminUserId);
        var transfer = await LoadTransferForUpdateAsync(transferId, cancellationToken);
        transfer.AdminNotes = NormalizeOptional(adminNotes, NotesMaxLength);
        transfer.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTransferAsync(transfer.Id, cancellationToken);
        await LogAsync(AuditActions.DogTransferAdminNoteUpdated, saved, adminUserId, $"Admin notes updated for transfer #{saved.Id}.");
        return ToDetailsDto(saved, shelterId: null, isAdmin: true);
    }

    public async Task<IReadOnlyList<DogTransferHistoryItemDto>> GetDogTransferHistoryAsync(
        int dogId,
        int? shelterId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().Where(transfer => transfer.DogId == dogId);
        if (!isAdmin)
        {
            if (!shelterId.HasValue)
            {
                return [];
            }

            query = query.Where(transfer => transfer.SourceShelterId == shelterId.Value || transfer.DestinationShelterId == shelterId.Value);
        }

        var transfers = await query
            .OrderByDescending(transfer => transfer.RequestedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return transfers.Select(ToHistoryDto).ToList();
    }

    public async Task<DogTransferStatsDto> GetTransferStatsAsync(
        int? shelterId = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.DogTransferRequests.AsNoTracking();
        if (shelterId.HasValue)
        {
            query = query.Where(transfer => transfer.SourceShelterId == shelterId.Value || transfer.DestinationShelterId == shelterId.Value);
        }

        var transfers = await query.ToListAsync(cancellationToken);
        return new DogTransferStatsDto(
            IncomingPending: shelterId.HasValue ? transfers.Count(t => t.DestinationShelterId == shelterId.Value && t.Status == DogTransferStatus.Pending) : transfers.Count(t => t.Status == DogTransferStatus.Pending),
            OutgoingPending: shelterId.HasValue ? transfers.Count(t => t.SourceShelterId == shelterId.Value && t.Status == DogTransferStatus.Pending) : transfers.Count(t => t.Status == DogTransferStatus.Pending),
            ApprovedWaitingCompletion: transfers.Count(t => t.Status == DogTransferStatus.Approved),
            Completed: transfers.Count(t => t.Status == DogTransferStatus.Completed),
            UrgentRequests: transfers.Count(t => t.Priority == DogTransferPriority.Urgent && t.Status is DogTransferStatus.Pending or DogTransferStatus.Approved),
            Total: transfers.Count);
    }

    private async Task<DogTransferDetailsDto> RespondAsync(
        int transferId,
        int destinationShelterId,
        string respondedByUserId,
        DogTransferStatus newStatus,
        DogTransferDecisionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureUserId(respondedByUserId);
        var transfer = await LoadTransferForUpdateAsync(transferId, cancellationToken);
        if (transfer.DestinationShelterId != destinationShelterId)
        {
            throw new InvalidOperationException("Only the destination shelter can respond to this transfer request.");
        }

        if (transfer.Status != DogTransferStatus.Pending)
        {
            throw new InvalidOperationException("Only pending transfer requests can be approved or rejected.");
        }

        var now = DateTime.UtcNow;
        transfer.Status = newStatus;
        transfer.RespondedByUserId = respondedByUserId;
        transfer.RespondedAtUtc = now;
        transfer.UpdatedAtUtc = now;
        transfer.DestinationShelterResponseNotes = NormalizeOptional(request.Notes, NotesMaxLength);

        await context.SaveChangesAsync(cancellationToken);
        var saved = await LoadRequiredTransferAsync(transfer.Id, cancellationToken);
        if (newStatus == DogTransferStatus.Approved)
        {
            await NotifyTransferApprovedAsync(saved);
            await LogAsync(AuditActions.DogTransferApproved, saved, respondedByUserId, $"Transfer approved for dog {saved.Dog?.Name}.");
        }
        else
        {
            await NotifyTransferRejectedAsync(saved);
            await LogAsync(AuditActions.DogTransferRejected, saved, respondedByUserId, $"Transfer rejected for dog {saved.Dog?.Name}.");
        }

        return ToDetailsDto(saved, destinationShelterId, isAdmin: false);
    }

    private IQueryable<DogTransferRequest> BaseQuery()
    {
        return context.DogTransferRequests
            .Include(transfer => transfer.Dog)
                .ThenInclude(dog => dog!.DogBreed)
            .Include(transfer => transfer.Dog)
                .ThenInclude(dog => dog!.SecondaryBreed)
            .Include(transfer => transfer.SourceShelter)
            .Include(transfer => transfer.DestinationShelter)
            .Include(transfer => transfer.RequestedByUser)
            .Include(transfer => transfer.RespondedByUser)
            .Include(transfer => transfer.CompletedByUser)
            .AsSplitQuery();
    }

    private async Task<DogTransferRequest> LoadTransferForUpdateAsync(int transferId, CancellationToken cancellationToken)
    {
        return await context.DogTransferRequests
            .Include(transfer => transfer.Dog)
            .FirstOrDefaultAsync(transfer => transfer.Id == transferId, cancellationToken)
            ?? throw new InvalidOperationException("Transfer request was not found.");
    }

    private async Task<DogTransferRequest> LoadRequiredTransferAsync(int transferId, CancellationToken cancellationToken)
    {
        return await BaseQuery()
            .FirstOrDefaultAsync(transfer => transfer.Id == transferId, cancellationToken)
            ?? throw new InvalidOperationException("Transfer request was not found.");
    }

    private static bool CanView(DogTransferRequest transfer, int? shelterId, bool isAdmin)
    {
        return isAdmin || (shelterId.HasValue && (transfer.SourceShelterId == shelterId.Value || transfer.DestinationShelterId == shelterId.Value));
    }

    private static DogTransferRequestDto ToListDto(DogTransferRequest transfer, int? viewerShelterId, bool isAdmin)
    {
        return new DogTransferRequestDto(
            transfer.Id,
            transfer.DogId,
            transfer.Dog?.Name ?? "Unknown dog",
            transfer.Dog is null ? "Unknown" : DogBreedFormatter.Format(transfer.Dog),
            transfer.Status,
            transfer.Priority,
            transfer.SourceShelterId,
            transfer.SourceShelter?.Name ?? "Unknown shelter",
            transfer.DestinationShelterId,
            transfer.DestinationShelter?.Name ?? "Unknown shelter",
            Truncate(transfer.Reason, 140),
            transfer.RequestedAtUtc,
            transfer.RespondedAtUtc,
            transfer.CompletedAtUtc,
            transfer.CancelledAtUtc,
            CanApprove(transfer, viewerShelterId, isAdmin),
            CanReject(transfer, viewerShelterId, isAdmin),
            CanCancel(transfer, viewerShelterId, isAdmin),
            CanComplete(transfer, viewerShelterId, isAdmin));
    }

    private static DogTransferDetailsDto ToDetailsDto(DogTransferRequest transfer, int? shelterId, bool isAdmin)
    {
        return new DogTransferDetailsDto(
            transfer.Id,
            transfer.DogId,
            transfer.Dog?.Name ?? "Unknown dog",
            transfer.Dog is null ? "Unknown" : DogBreedFormatter.Format(transfer.Dog),
            transfer.Status,
            transfer.Priority,
            transfer.SourceShelterId,
            transfer.SourceShelter?.Name ?? "Unknown shelter",
            transfer.DestinationShelterId,
            transfer.DestinationShelter?.Name ?? "Unknown shelter",
            transfer.Reason,
            transfer.SourceShelterNotes,
            transfer.DestinationShelterResponseNotes,
            transfer.AdminNotes,
            GetUserDisplayName(transfer.RequestedByUser),
            transfer.RespondedByUser is null ? null : GetUserDisplayName(transfer.RespondedByUser),
            transfer.CompletedByUser is null ? null : GetUserDisplayName(transfer.CompletedByUser),
            transfer.RequestedAtUtc,
            transfer.RespondedAtUtc,
            transfer.CompletedAtUtc,
            transfer.CancelledAtUtc,
            transfer.UpdatedAtUtc,
            CanApprove(transfer, shelterId, isAdmin),
            CanReject(transfer, shelterId, isAdmin),
            CanCancel(transfer, shelterId, isAdmin),
            CanComplete(transfer, shelterId, isAdmin));
    }

    private static DogTransferHistoryItemDto ToHistoryDto(DogTransferRequest transfer)
    {
        return new DogTransferHistoryItemDto(
            transfer.Id,
            transfer.DogId,
            transfer.Dog?.Name ?? "Unknown dog",
            transfer.Status,
            transfer.Priority,
            transfer.SourceShelter?.Name ?? "Unknown shelter",
            transfer.DestinationShelter?.Name ?? "Unknown shelter",
            Truncate(transfer.Reason, 120),
            transfer.RequestedAtUtc,
            transfer.CompletedAtUtc,
            transfer.RespondedAtUtc,
            transfer.CancelledAtUtc);
    }

    private static bool CanApprove(DogTransferRequest transfer, int? shelterId, bool isAdmin)
    {
        return transfer.Status == DogTransferStatus.Pending && shelterId.HasValue && transfer.DestinationShelterId == shelterId.Value;
    }

    private static bool CanReject(DogTransferRequest transfer, int? shelterId, bool isAdmin)
    {
        return CanApprove(transfer, shelterId, isAdmin);
    }

    private static bool CanCancel(DogTransferRequest transfer, int? shelterId, bool isAdmin)
    {
        return transfer.Status == DogTransferStatus.Pending && (isAdmin || (shelterId.HasValue && transfer.SourceShelterId == shelterId.Value));
    }

    private static bool CanComplete(DogTransferRequest transfer, int? shelterId, bool isAdmin)
    {
        return transfer.Status == DogTransferStatus.Approved && (isAdmin || (shelterId.HasValue && (transfer.SourceShelterId == shelterId.Value || transfer.DestinationShelterId == shelterId.Value)));
    }

    private async Task NotifyTransferRequestedAsync(DogTransferRequest transfer)
    {
        await NotifyShelterAsync(
            transfer.DestinationShelter,
            "Incoming dog transfer request",
            $"{transfer.SourceShelter?.Name ?? "Another shelter"} requested to transfer {transfer.Dog?.Name ?? "a dog"} to your shelter.",
            NotificationType.Warning,
            "/shelter/transfers",
            transfer.Id);

        if (transfer.Priority == DogTransferPriority.Urgent)
        {
            await NotifyAdminsAsync(
                "Urgent dog transfer request",
                $"Urgent transfer requested for {transfer.Dog?.Name ?? "a dog"} from {transfer.SourceShelter?.Name} to {transfer.DestinationShelter?.Name}.",
                transfer.Id);
        }
    }

    private Task NotifyTransferApprovedAsync(DogTransferRequest transfer)
    {
        return NotifyShelterAsync(
            transfer.SourceShelter,
            "Dog transfer approved",
            $"{transfer.DestinationShelter?.Name ?? "The destination shelter"} approved the transfer for {transfer.Dog?.Name ?? "the dog"}.",
            NotificationType.Success,
            "/shelter/transfers",
            transfer.Id);
    }

    private Task NotifyTransferRejectedAsync(DogTransferRequest transfer)
    {
        return NotifyShelterAsync(
            transfer.SourceShelter,
            "Dog transfer rejected",
            $"{transfer.DestinationShelter?.Name ?? "The destination shelter"} rejected the transfer for {transfer.Dog?.Name ?? "the dog"}.",
            NotificationType.Warning,
            "/shelter/transfers",
            transfer.Id);
    }

    private Task NotifyTransferCancelledAsync(DogTransferRequest transfer)
    {
        return NotifyShelterAsync(
            transfer.DestinationShelter,
            "Dog transfer cancelled",
            $"{transfer.SourceShelter?.Name ?? "The source shelter"} cancelled the transfer request for {transfer.Dog?.Name ?? "the dog"}.",
            NotificationType.Info,
            "/shelter/transfers",
            transfer.Id);
    }

    private async Task NotifyTransferCompletedAsync(DogTransferRequest transfer)
    {
        await NotifyShelterAsync(
            transfer.SourceShelter,
            "Dog transfer completed",
            $"{transfer.Dog?.Name ?? "The dog"} has been transferred to {transfer.DestinationShelter?.Name ?? "the destination shelter"}.",
            NotificationType.Success,
            "/shelter/transfers",
            transfer.Id);

        await NotifyShelterAsync(
            transfer.DestinationShelter,
            "Dog transfer completed",
            $"{transfer.Dog?.Name ?? "The dog"} is now assigned to your shelter.",
            NotificationType.Success,
            "/shelter/transfers",
            transfer.Id);
    }

    private async Task NotifyShelterAsync(Shelter? shelter, string title, string message, NotificationType type, string link, int transferId)
    {
        if (notificationService is null || string.IsNullOrWhiteSpace(shelter?.ApplicationUserId))
        {
            return;
        }

        await notificationService.CreateNotificationAsync(
            shelter.ApplicationUserId,
            title,
            message,
            NotificationCategory.Transfer,
            type,
            link,
            nameof(DogTransferRequest),
            transferId.ToString(),
            TimeSpan.FromMinutes(5));
    }

    private async Task NotifyAdminsAsync(string title, string message, int transferId)
    {
        if (notificationService is null)
        {
            return;
        }

        var adminRoleIds = await context.Roles
            .Where(role => role.Name == IdentitySeedData.AdminRole)
            .Select(role => role.Id)
            .ToListAsync();

        var adminUserIds = await context.UserRoles
            .Where(userRole => adminRoleIds.Contains(userRole.RoleId))
            .Select(userRole => userRole.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var adminUserId in adminUserIds)
        {
            await notificationService.CreateNotificationAsync(
                adminUserId,
                title,
                message,
                NotificationCategory.Transfer,
                NotificationType.Warning,
                "/admin/transfers",
                nameof(DogTransferRequest),
                transferId.ToString(),
                TimeSpan.FromMinutes(5));
        }
    }

    private Task LogAsync(string action, DogTransferRequest transfer, string userId, string description)
    {
        return auditLogService?.LogAsync(
            action,
            nameof(DogTransferRequest),
            transfer.Id.ToString(),
            description,
            userId: userId,
            additionalData: $"DogId={transfer.DogId};SourceShelterId={transfer.SourceShelterId};DestinationShelterId={transfer.DestinationShelterId};Status={transfer.Status};Priority={transfer.Priority}") ?? Task.CompletedTask;
    }

    private async Task RefreshDogSearchEmbeddingBestEffortAsync(int dogId)
    {
        if (dogSearchEmbeddingService is null)
        {
            return;
        }

        try
        {
            await dogSearchEmbeddingService.RefreshDogEmbeddingAsync(dogId);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Dog search embedding refresh failed after transfer completion for DogId {DogId}.", dogId);
        }
    }

    private static string NormalizeRequired(string? value, string errorMessage, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"Value must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"Notes must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? MergeNotes(string? existing, string label, string? note)
    {
        var normalizedNote = NormalizeOptional(note, NotesMaxLength);
        if (string.IsNullOrWhiteSpace(normalizedNote))
        {
            return existing;
        }

        var merged = string.IsNullOrWhiteSpace(existing)
            ? $"{label}: {normalizedNote}"
            : $"{existing.Trim()}\n{label}: {normalizedNote}";

        return merged.Length <= NotesMaxLength ? merged : merged[..NotesMaxLength];
    }

    private static void EnsureUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : $"{normalized[..Math.Max(0, maxLength - 3)]}...";
    }

    private static string GetUserDisplayName(ApplicationUser? user)
    {
        if (user is null)
        {
            return "Unknown user";
        }

        return string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.UserName ?? "Unknown user" : user.FullName;
    }
}
