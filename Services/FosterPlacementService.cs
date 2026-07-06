using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class FosterPlacementService(
    ApplicationDbContext context,
    INotificationService? notificationService = null,
    IAuditLogService? auditLogService = null) : IFosterPlacementService
{
    private static readonly FosterPlacementStatus[] OpenStatuses =
    [
        FosterPlacementStatus.Pending,
        FosterPlacementStatus.Approved,
        FosterPlacementStatus.Active
    ];

    private const int ShortTextMaxLength = 250;
    private const int NotesMaxLength = 1000;

    public async Task<IReadOnlyList<FosterCaregiverProfileDto>> GetCaregiversForShelterAsync(
        int shelterId,
        CancellationToken cancellationToken = default)
    {
        var caregivers = await context.FosterCaregiverProfiles
            .Include(caregiver => caregiver.PreferredShelter)
            .Where(caregiver => caregiver.PreferredShelterId == shelterId)
            .OrderByDescending(caregiver => caregiver.IsActive)
            .ThenBy(caregiver => caregiver.DisplayName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return caregivers.Select(ToCaregiverDto).ToList();
    }

    public async Task<IReadOnlyList<FosterCaregiverProfileDto>> GetAvailableCaregiversAsync(
        int? shelterId = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.FosterCaregiverProfiles
            .Include(caregiver => caregiver.PreferredShelter)
            .Where(caregiver => caregiver.IsActive && caregiver.ActivePlacementCount < caregiver.Capacity);

        if (shelterId.HasValue)
        {
            query = query.Where(caregiver => caregiver.PreferredShelterId == shelterId.Value);
        }

        var caregivers = await query
            .OrderBy(caregiver => caregiver.DisplayName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return caregivers.Select(ToCaregiverDto).ToList();
    }

    public async Task<FosterCaregiverProfileDto> CreateCaregiverAsync(
        FosterCaregiverCreateRequest request,
        string currentUserId,
        int? shelterId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(currentUserId);
        var preferredShelterId = request.PreferredShelterId ?? shelterId;
        EnsureShelterScope(preferredShelterId, shelterId);

        var caregiver = new FosterCaregiverProfile
        {
            DisplayName = NormalizeRequired(request.DisplayName, "Caregiver name is required.", 120),
            Email = NormalizeRequired(request.Email, "Caregiver email is required.", 256),
            PhoneNumber = NormalizeOptional(request.PhoneNumber, 40),
            AddressSummary = NormalizeOptional(request.AddressSummary, ShortTextMaxLength),
            PreferredShelterId = preferredShelterId,
            ExperienceNotes = NormalizeOptional(request.ExperienceNotes, NotesMaxLength),
            HomeEnvironmentNotes = NormalizeOptional(request.HomeEnvironmentNotes, NotesMaxLength),
            Capacity = ValidateCapacity(request.Capacity),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        context.FosterCaregiverProfiles.Add(caregiver);
        await context.SaveChangesAsync(cancellationToken);
        var saved = await LoadCaregiverAsync(caregiver.Id, cancellationToken);
        await LogAsync(AuditActions.FosterCaregiverCreated, nameof(FosterCaregiverProfile), caregiver.Id, currentUserId, $"Foster caregiver {saved.DisplayName} created.");
        return ToCaregiverDto(saved);
    }

    public async Task<FosterCaregiverProfileDto> UpdateCaregiverAsync(
        int caregiverId,
        FosterCaregiverUpdateRequest request,
        string currentUserId,
        int? shelterId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(currentUserId);
        var caregiver = await context.FosterCaregiverProfiles
            .FirstOrDefaultAsync(caregiver => caregiver.Id == caregiverId, cancellationToken)
            ?? throw new InvalidOperationException("Foster caregiver was not found.");

        if (!isAdmin && caregiver.PreferredShelterId != shelterId)
        {
            throw new InvalidOperationException("You can only manage caregivers linked to your shelter.");
        }

        EnsureShelterScope(request.PreferredShelterId, isAdmin ? null : shelterId);
        var capacity = ValidateCapacity(request.Capacity);
        if (capacity < caregiver.ActivePlacementCount)
        {
            throw new InvalidOperationException("Capacity cannot be lower than the caregiver's active placements.");
        }

        caregiver.DisplayName = NormalizeRequired(request.DisplayName, "Caregiver name is required.", 120);
        caregiver.Email = NormalizeRequired(request.Email, "Caregiver email is required.", 256);
        caregiver.PhoneNumber = NormalizeOptional(request.PhoneNumber, 40);
        caregiver.AddressSummary = NormalizeOptional(request.AddressSummary, ShortTextMaxLength);
        caregiver.PreferredShelterId = request.PreferredShelterId;
        caregiver.ExperienceNotes = NormalizeOptional(request.ExperienceNotes, NotesMaxLength);
        caregiver.HomeEnvironmentNotes = NormalizeOptional(request.HomeEnvironmentNotes, NotesMaxLength);
        caregiver.Capacity = capacity;
        caregiver.IsActive = request.IsActive;
        caregiver.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        var saved = await LoadCaregiverAsync(caregiver.Id, cancellationToken);
        await LogAsync(AuditActions.FosterCaregiverUpdated, nameof(FosterCaregiverProfile), caregiver.Id, currentUserId, $"Foster caregiver {saved.DisplayName} updated.");
        return ToCaregiverDto(saved);
    }

    public async Task<IReadOnlyList<FosterPlacementDto>> GetShelterPlacementsAsync(
        int shelterId,
        FosterPlacementFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var placements = await ApplyPlacementFilter(BasePlacementQuery().Where(placement => placement.ShelterId == shelterId), filter)
            .OrderByDescending(placement => placement.Status == FosterPlacementStatus.Active)
            .ThenByDescending(placement => placement.Status == FosterPlacementStatus.Pending || placement.Status == FosterPlacementStatus.Approved)
            .ThenBy(placement => placement.PlannedEndDateUtc ?? DateTime.MaxValue)
            .ThenByDescending(placement => placement.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return placements.Select(placement => ToListDto(placement, shelterId, isAdmin: false)).ToList();
    }

    public async Task<IReadOnlyList<FosterPlacementDto>> GetAdminPlacementsAsync(
        FosterPlacementFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var placements = await ApplyPlacementFilter(BasePlacementQuery(), filter)
            .OrderByDescending(placement => placement.Status == FosterPlacementStatus.Active)
            .ThenByDescending(placement => placement.Priority)
            .ThenBy(placement => placement.PlannedEndDateUtc ?? DateTime.MaxValue)
            .ThenByDescending(placement => placement.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return placements.Select(placement => ToListDto(placement, viewerShelterId: null, isAdmin: true)).ToList();
    }

    public async Task<FosterPlacementDetailsDto?> GetPlacementDetailsAsync(
        int placementId,
        int? shelterId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var placement = await BasePlacementQuery()
            .Include(placement => placement.Activities)
                .ThenInclude(activity => activity.ActorUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(placement => placement.Id == placementId, cancellationToken);

        return placement is null || !CanView(placement, shelterId, isAdmin)
            ? null
            : ToDetailsDto(placement, shelterId, isAdmin);
    }

    public async Task<FosterPlacementDto> CreatePlacementAsync(
        int shelterId,
        string createdByUserId,
        FosterPlacementCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(createdByUserId);
        var startDate = NormalizeUtc(request.StartDateUtc);
        var plannedEndDate = NormalizeUtc(request.PlannedEndDateUtc);
        ValidateDateRange(startDate, plannedEndDate);

        var dog = await context.Dogs
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .FirstOrDefaultAsync(dog => dog.Id == request.DogId, cancellationToken);
        if (dog is null || dog.ShelterId != shelterId)
        {
            throw new InvalidOperationException("Dog was not found for your shelter.");
        }

        if (dog.Status == DogStatus.Adopted)
        {
            throw new InvalidOperationException("Adopted dogs cannot be assigned to foster care.");
        }

        var caregiver = await LoadCaregiverForAssignmentAsync(request.FosterCaregiverProfileId, shelterId, cancellationToken);
        await EnsureDogHasNoOpenPlacementAsync(dog.Id, cancellationToken);
        EnsureCaregiverHasCapacity(caregiver);

        var now = DateTime.UtcNow;
        var placement = new FosterPlacement
        {
            DogId = dog.Id,
            ShelterId = shelterId,
            FosterCaregiverProfileId = caregiver.Id,
            CreatedByUserId = createdByUserId,
            Status = FosterPlacementStatus.Pending,
            Priority = request.Priority,
            Reason = request.Reason,
            StartDateUtc = startDate,
            PlannedEndDateUtc = plannedEndDate,
            CareInstructions = NormalizeOptional(request.CareInstructions, NotesMaxLength),
            MedicalNotesSummary = NormalizeOptional(request.MedicalNotesSummary, NotesMaxLength),
            ShelterNotes = NormalizeOptional(request.ShelterNotes, NotesMaxLength),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        context.FosterPlacements.Add(placement);
        await context.SaveChangesAsync(cancellationToken);
        await AddActivityAsync(placement.Id, FosterPlacementActivityType.Created, createdByUserId, "Foster placement created.", cancellationToken);

        var saved = await LoadRequiredPlacementAsync(placement.Id, cancellationToken);
        await NotifyPlacementCreatedAsync(saved);
        await LogPlacementAsync(AuditActions.FosterPlacementCreated, saved, createdByUserId, $"Foster placement created for dog {saved.Dog?.Name}.");
        return ToListDto(saved, shelterId, isAdmin: false);
    }

    public async Task<FosterPlacementDetailsDto> UpdatePlacementAsync(
        int placementId,
        int shelterId,
        string currentUserId,
        FosterPlacementUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(currentUserId);
        var placement = await LoadPlacementForUpdateAsync(placementId, cancellationToken);
        EnsureShelterOwnsPlacement(placement, shelterId);
        if (placement.Status is not (FosterPlacementStatus.Pending or FosterPlacementStatus.Approved))
        {
            throw new InvalidOperationException("Only pending or approved foster placements can be edited.");
        }

        var startDate = NormalizeUtc(request.StartDateUtc);
        var plannedEndDate = NormalizeUtc(request.PlannedEndDateUtc);
        ValidateDateRange(startDate, plannedEndDate);

        placement.Priority = request.Priority;
        placement.Reason = request.Reason;
        placement.StartDateUtc = startDate;
        placement.PlannedEndDateUtc = plannedEndDate;
        placement.CareInstructions = NormalizeOptional(request.CareInstructions, NotesMaxLength);
        placement.MedicalNotesSummary = NormalizeOptional(request.MedicalNotesSummary, NotesMaxLength);
        placement.ShelterNotes = NormalizeOptional(request.ShelterNotes, NotesMaxLength);
        placement.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await AddActivityAsync(placement.Id, FosterPlacementActivityType.Updated, currentUserId, "Placement details updated.", cancellationToken);
        var saved = await LoadRequiredPlacementAsync(placement.Id, cancellationToken);
        await LogPlacementAsync(AuditActions.FosterPlacementUpdated, saved, currentUserId, $"Foster placement updated for dog {saved.Dog?.Name}.");
        return ToDetailsDto(saved, shelterId, isAdmin: false);
    }

    public Task<FosterPlacementDetailsDto> ApprovePlacementAsync(
        int placementId,
        int shelterId,
        string approvedByUserId,
        FosterPlacementDecisionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        return ChangePlacementStatusAsync(
            placementId,
            shelterId,
            approvedByUserId,
            FosterPlacementStatus.Pending,
            FosterPlacementStatus.Approved,
            AuditActions.FosterPlacementApproved,
            FosterPlacementActivityType.Approved,
            request?.Notes,
            "Foster placement approved.",
            cancellationToken);
    }

    public async Task<FosterPlacementDetailsDto> StartPlacementAsync(
        int placementId,
        int shelterId,
        string startedByUserId,
        FosterPlacementStartRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(startedByUserId);
        var placement = await LoadPlacementForUpdateAsync(placementId, cancellationToken);
        EnsureShelterOwnsPlacement(placement, shelterId);
        if (placement.Status != FosterPlacementStatus.Approved)
        {
            throw new InvalidOperationException("Only approved foster placements can be started.");
        }

        if (placement.FosterCaregiverProfile is null || !placement.FosterCaregiverProfile.IsActive)
        {
            throw new InvalidOperationException("Foster caregiver must be active before starting a placement.");
        }

        EnsureCaregiverHasCapacity(placement.FosterCaregiverProfile);
        placement.Status = FosterPlacementStatus.Active;
        placement.FosterCaregiverProfile.ActivePlacementCount++;
        placement.UpdatedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request?.Notes))
        {
            placement.ShelterNotes = MergeNotes(placement.ShelterNotes, "Start note", request.Notes);
        }

        await context.SaveChangesAsync(cancellationToken);
        await AddActivityAsync(placement.Id, FosterPlacementActivityType.Started, startedByUserId, "Foster placement started.", cancellationToken);
        var saved = await LoadRequiredPlacementAsync(placement.Id, cancellationToken);
        await NotifyPlacementStartedAsync(saved);
        await LogPlacementAsync(AuditActions.FosterPlacementStarted, saved, startedByUserId, $"Foster placement started for dog {saved.Dog?.Name}.");
        return ToDetailsDto(saved, shelterId, isAdmin: false);
    }

    public async Task<FosterPlacementDetailsDto> CompletePlacementAsync(
        int placementId,
        int? shelterId,
        string completedByUserId,
        bool isAdmin,
        FosterPlacementCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(completedByUserId);
        var placement = await LoadPlacementForUpdateAsync(placementId, cancellationToken);
        if (!CanManage(placement, shelterId, isAdmin))
        {
            throw new InvalidOperationException("You cannot manage this foster placement.");
        }

        if (placement.Status != FosterPlacementStatus.Active)
        {
            throw new InvalidOperationException("Only active foster placements can be completed.");
        }

        var actualEnd = NormalizeUtc(request.ActualEndDateUtc);
        if (actualEnd < placement.StartDateUtc)
        {
            throw new InvalidOperationException("Actual end date must be after the start date.");
        }

        placement.Status = FosterPlacementStatus.Completed;
        placement.ActualEndDateUtc = actualEnd;
        placement.EndedByUserId = completedByUserId;
        placement.CompletionNotes = NormalizeOptional(request.CompletionNotes, NotesMaxLength);
        placement.FosterNotes = NormalizeOptional(request.FosterNotes, NotesMaxLength);
        placement.UpdatedAtUtc = DateTime.UtcNow;
        if (placement.FosterCaregiverProfile is not null && placement.FosterCaregiverProfile.ActivePlacementCount > 0)
        {
            placement.FosterCaregiverProfile.ActivePlacementCount--;
        }

        await context.SaveChangesAsync(cancellationToken);
        await AddActivityAsync(placement.Id, FosterPlacementActivityType.Completed, completedByUserId, "Foster placement completed.", cancellationToken);
        var saved = await LoadRequiredPlacementAsync(placement.Id, cancellationToken);
        await NotifyPlacementCompletedAsync(saved);
        await LogPlacementAsync(AuditActions.FosterPlacementCompleted, saved, completedByUserId, $"Foster placement completed for dog {saved.Dog?.Name}.");
        return ToDetailsDto(saved, shelterId, isAdmin);
    }

    public async Task<FosterPlacementDetailsDto> CancelPlacementAsync(
        int placementId,
        int? shelterId,
        string cancelledByUserId,
        bool isAdmin = false,
        FosterPlacementDecisionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(cancelledByUserId);
        var placement = await LoadPlacementForUpdateAsync(placementId, cancellationToken);
        if (!CanManage(placement, shelterId, isAdmin))
        {
            throw new InvalidOperationException("You cannot manage this foster placement.");
        }

        if (placement.Status is not (FosterPlacementStatus.Pending or FosterPlacementStatus.Approved))
        {
            throw new InvalidOperationException("Only pending or approved foster placements can be cancelled.");
        }

        placement.Status = FosterPlacementStatus.Cancelled;
        placement.EndedByUserId = cancelledByUserId;
        placement.ActualEndDateUtc = DateTime.UtcNow;
        placement.UpdatedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request?.Notes))
        {
            placement.CompletionNotes = NormalizeOptional(request.Notes, NotesMaxLength);
        }

        await context.SaveChangesAsync(cancellationToken);
        await AddActivityAsync(placement.Id, FosterPlacementActivityType.Cancelled, cancelledByUserId, "Foster placement cancelled.", cancellationToken);
        var saved = await LoadRequiredPlacementAsync(placement.Id, cancellationToken);
        await NotifyPlacementCancelledAsync(saved);
        await LogPlacementAsync(AuditActions.FosterPlacementCancelled, saved, cancelledByUserId, $"Foster placement cancelled for dog {saved.Dog?.Name}.");
        return ToDetailsDto(saved, shelterId, isAdmin);
    }

    public async Task<FosterPlacementDetailsDto> AddPlacementNoteAsync(
        int placementId,
        int shelterId,
        string currentUserId,
        FosterPlacementNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(currentUserId);
        var note = NormalizeRequired(request.Notes, "Note is required.", NotesMaxLength);
        var placement = await LoadPlacementForUpdateAsync(placementId, cancellationToken);
        EnsureShelterOwnsPlacement(placement, shelterId);
        if (placement.Status is FosterPlacementStatus.Completed or FosterPlacementStatus.Cancelled)
        {
            throw new InvalidOperationException("Completed or cancelled placements are read-only.");
        }

        placement.ShelterNotes = MergeNotes(placement.ShelterNotes, "Shelter note", note);
        placement.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await AddActivityAsync(placement.Id, FosterPlacementActivityType.NoteAdded, currentUserId, "Shelter note added.", cancellationToken);
        var saved = await LoadRequiredPlacementAsync(placement.Id, cancellationToken);
        await LogPlacementAsync(AuditActions.FosterPlacementNoteAdded, saved, currentUserId, $"Foster placement note added for dog {saved.Dog?.Name}.");
        return ToDetailsDto(saved, shelterId, isAdmin: false);
    }

    public async Task<IReadOnlyList<DogFosterHistoryItemDto>> GetDogFosterHistoryAsync(
        int dogId,
        int? shelterId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var query = BasePlacementQuery().Where(placement => placement.DogId == dogId);
        if (!isAdmin)
        {
            if (!shelterId.HasValue)
            {
                return [];
            }

            query = query.Where(placement => placement.ShelterId == shelterId.Value);
        }

        var placements = await query
            .OrderByDescending(placement => placement.StartDateUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return placements.Select(ToHistoryDto).ToList();
    }

    public async Task<IReadOnlyList<FosterPlacementDto>> GetCaregiverPlacementsAsync(
        int caregiverId,
        int? shelterId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var query = BasePlacementQuery().Where(placement => placement.FosterCaregiverProfileId == caregiverId);
        if (!isAdmin)
        {
            if (!shelterId.HasValue)
            {
                return [];
            }

            query = query.Where(placement => placement.ShelterId == shelterId.Value);
        }

        var placements = await query
            .OrderByDescending(placement => placement.StartDateUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return placements.Select(placement => ToListDto(placement, shelterId, isAdmin)).ToList();
    }

    public async Task<FosterPlacementStatsDto> GetFosterStatsAsync(
        int? shelterId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var endingSoonCutoff = now.AddDays(7);
        var placementsQuery = context.FosterPlacements.AsNoTracking();
        var caregiversQuery = context.FosterCaregiverProfiles.AsNoTracking();

        if (shelterId.HasValue)
        {
            placementsQuery = placementsQuery.Where(placement => placement.ShelterId == shelterId.Value);
            caregiversQuery = caregiversQuery.Where(caregiver => caregiver.PreferredShelterId == shelterId.Value);
        }

        var placements = await placementsQuery.ToListAsync(cancellationToken);
        var availableCaregivers = await caregiversQuery
            .CountAsync(caregiver => caregiver.IsActive && caregiver.ActivePlacementCount < caregiver.Capacity, cancellationToken);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return new FosterPlacementStatsDto(
            Pending: placements.Count(placement => placement.Status == FosterPlacementStatus.Pending),
            Approved: placements.Count(placement => placement.Status == FosterPlacementStatus.Approved),
            Active: placements.Count(placement => placement.Status == FosterPlacementStatus.Active),
            EndingSoon: placements.Count(placement => placement.Status == FosterPlacementStatus.Active && placement.PlannedEndDateUtc is { } end && end >= now && end <= endingSoonCutoff),
            Overdue: placements.Count(placement => placement.Status == FosterPlacementStatus.Active && placement.PlannedEndDateUtc is { } end && end < now),
            CompletedThisMonth: placements.Count(placement => placement.Status == FosterPlacementStatus.Completed && placement.ActualEndDateUtc >= monthStart),
            AvailableCaregivers: availableCaregivers,
            Total: placements.Count);
    }

    private async Task<FosterPlacementDetailsDto> ChangePlacementStatusAsync(
        int placementId,
        int shelterId,
        string userId,
        FosterPlacementStatus requiredStatus,
        FosterPlacementStatus newStatus,
        string auditAction,
        FosterPlacementActivityType activityType,
        string? note,
        string activityMessage,
        CancellationToken cancellationToken)
    {
        EnsureUserId(userId);
        var placement = await LoadPlacementForUpdateAsync(placementId, cancellationToken);
        EnsureShelterOwnsPlacement(placement, shelterId);
        if (placement.Status != requiredStatus)
        {
            throw new InvalidOperationException($"Only {FormatStatus(requiredStatus)} foster placements can be {FormatStatus(newStatus).ToLowerInvariant()}.");
        }

        placement.Status = newStatus;
        placement.ApprovedByUserId = newStatus == FosterPlacementStatus.Approved ? userId : placement.ApprovedByUserId;
        placement.UpdatedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
        {
            placement.ShelterNotes = MergeNotes(placement.ShelterNotes, FormatStatus(newStatus), note);
        }

        await context.SaveChangesAsync(cancellationToken);
        await AddActivityAsync(placement.Id, activityType, userId, activityMessage, cancellationToken);
        var saved = await LoadRequiredPlacementAsync(placement.Id, cancellationToken);
        await NotifyPlacementStatusChangedAsync(saved, newStatus);
        await LogPlacementAsync(auditAction, saved, userId, activityMessage);
        return ToDetailsDto(saved, shelterId, isAdmin: false);
    }

    private IQueryable<FosterPlacement> BasePlacementQuery()
    {
        return context.FosterPlacements
            .Include(placement => placement.Dog)
                .ThenInclude(dog => dog!.DogBreed)
            .Include(placement => placement.Dog)
                .ThenInclude(dog => dog!.SecondaryBreed)
            .Include(placement => placement.Shelter)
            .Include(placement => placement.FosterCaregiverProfile)
            .Include(placement => placement.CreatedByUser)
            .Include(placement => placement.ApprovedByUser)
            .Include(placement => placement.EndedByUser)
            .AsSplitQuery();
    }

    private static IQueryable<FosterPlacement> ApplyPlacementFilter(
        IQueryable<FosterPlacement> query,
        FosterPlacementFilterDto? filter)
    {
        filter ??= new FosterPlacementFilterDto();
        if (filter.Status.HasValue)
        {
            query = query.Where(placement => placement.Status == filter.Status.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(placement => placement.Priority == filter.Priority.Value);
        }

        if (filter.Reason.HasValue)
        {
            query = query.Where(placement => placement.Reason == filter.Reason.Value);
        }

        if (filter.ShelterId.HasValue)
        {
            query = query.Where(placement => placement.ShelterId == filter.ShelterId.Value);
        }

        if (filter.CaregiverId.HasValue)
        {
            query = query.Where(placement => placement.FosterCaregiverProfileId == filter.CaregiverId.Value);
        }

        if (filter.DogId.HasValue)
        {
            query = query.Where(placement => placement.DogId == filter.DogId.Value);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(placement => placement.StartDateUtc >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(placement => placement.StartDateUtc <= filter.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var normalizedSearch = filter.Search.Trim().ToUpper();
            query = query.Where(placement =>
                placement.Dog != null && placement.Dog.Name.ToUpper().Contains(normalizedSearch) ||
                placement.FosterCaregiverProfile != null && placement.FosterCaregiverProfile.DisplayName.ToUpper().Contains(normalizedSearch) ||
                placement.Shelter != null && placement.Shelter.Name.ToUpper().Contains(normalizedSearch) ||
                placement.CareInstructions != null && placement.CareInstructions.ToUpper().Contains(normalizedSearch) ||
                placement.ShelterNotes != null && placement.ShelterNotes.ToUpper().Contains(normalizedSearch));
        }

        return query;
    }

    private async Task<FosterPlacement> LoadPlacementForUpdateAsync(int placementId, CancellationToken cancellationToken)
    {
        return await context.FosterPlacements
            .Include(placement => placement.Dog)
            .Include(placement => placement.FosterCaregiverProfile)
            .FirstOrDefaultAsync(placement => placement.Id == placementId, cancellationToken)
            ?? throw new InvalidOperationException("Foster placement was not found.");
    }

    private async Task<FosterPlacement> LoadRequiredPlacementAsync(int placementId, CancellationToken cancellationToken)
    {
        return await BasePlacementQuery()
            .Include(placement => placement.Activities)
                .ThenInclude(activity => activity.ActorUser)
            .FirstOrDefaultAsync(placement => placement.Id == placementId, cancellationToken)
            ?? throw new InvalidOperationException("Foster placement was not found.");
    }

    private async Task<FosterCaregiverProfile> LoadCaregiverAsync(int caregiverId, CancellationToken cancellationToken)
    {
        return await context.FosterCaregiverProfiles
            .Include(caregiver => caregiver.PreferredShelter)
            .AsNoTracking()
            .FirstOrDefaultAsync(caregiver => caregiver.Id == caregiverId, cancellationToken)
            ?? throw new InvalidOperationException("Foster caregiver was not found.");
    }

    private async Task<FosterCaregiverProfile> LoadCaregiverForAssignmentAsync(
        int caregiverId,
        int shelterId,
        CancellationToken cancellationToken)
    {
        var caregiver = await context.FosterCaregiverProfiles
            .FirstOrDefaultAsync(caregiver => caregiver.Id == caregiverId, cancellationToken)
            ?? throw new InvalidOperationException("Foster caregiver was not found.");

        if (!caregiver.IsActive)
        {
            throw new InvalidOperationException("Foster caregiver must be active before assignment.");
        }

        if (caregiver.PreferredShelterId != shelterId)
        {
            throw new InvalidOperationException("Foster caregiver is not linked to your shelter.");
        }

        return caregiver;
    }

    private async Task EnsureDogHasNoOpenPlacementAsync(int dogId, CancellationToken cancellationToken)
    {
        var hasOpenPlacement = await context.FosterPlacements
            .AnyAsync(placement => placement.DogId == dogId && OpenStatuses.Contains(placement.Status), cancellationToken);
        if (hasOpenPlacement)
        {
            throw new InvalidOperationException("This dog already has an open foster placement.");
        }
    }

    private static void EnsureCaregiverHasCapacity(FosterCaregiverProfile caregiver)
    {
        if (!caregiver.IsActive)
        {
            throw new InvalidOperationException("Foster caregiver must be active before assignment.");
        }

        if (caregiver.ActivePlacementCount >= caregiver.Capacity)
        {
            throw new InvalidOperationException("Foster caregiver has reached their active placement capacity.");
        }
    }

    private static void EnsureShelterScope(int? preferredShelterId, int? currentShelterId)
    {
        if (currentShelterId.HasValue && preferredShelterId != currentShelterId.Value)
        {
            throw new InvalidOperationException("Caregiver must be linked to your shelter.");
        }
    }

    private static void EnsureShelterOwnsPlacement(FosterPlacement placement, int shelterId)
    {
        if (placement.ShelterId != shelterId)
        {
            throw new InvalidOperationException("You can only manage foster placements for your shelter.");
        }
    }

    private static bool CanView(FosterPlacement placement, int? shelterId, bool isAdmin)
    {
        return isAdmin || (shelterId.HasValue && placement.ShelterId == shelterId.Value);
    }

    private static bool CanManage(FosterPlacement placement, int? shelterId, bool isAdmin)
    {
        return isAdmin || (shelterId.HasValue && placement.ShelterId == shelterId.Value);
    }

    private async Task AddActivityAsync(
        int placementId,
        FosterPlacementActivityType activityType,
        string? actorUserId,
        string message,
        CancellationToken cancellationToken)
    {
        context.FosterPlacementActivities.Add(new FosterPlacementActivity
        {
            FosterPlacementId = placementId,
            ActivityType = activityType,
            ActorUserId = actorUserId,
            Message = NormalizeRequired(message, "Activity message is required.", NotesMaxLength),
            CreatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyPlacementCreatedAsync(FosterPlacement placement)
    {
        await NotifyShelterAsync(
            placement.Shelter,
            "Foster placement created",
            $"{placement.Dog?.Name ?? "A dog"} has a new foster placement request.",
            NotificationType.Info,
            "/shelter/foster-placements",
            placement.Id);

        await NotifyCaregiverAsync(
            placement,
            "Foster placement assigned",
            $"{placement.Dog?.Name ?? "A dog"} has been assigned to you as a pending foster placement.");

        if (placement.Priority == FosterPlacementPriority.Urgent)
        {
            await NotifyAdminsAsync(
                "Urgent foster placement",
                $"Urgent foster placement created for {placement.Dog?.Name ?? "a dog"} at {placement.Shelter?.Name}.",
                placement.Id);
        }
    }

    private Task NotifyPlacementStartedAsync(FosterPlacement placement)
    {
        return NotifyPlacementStatusChangedAsync(placement, FosterPlacementStatus.Active);
    }

    private Task NotifyPlacementCompletedAsync(FosterPlacement placement)
    {
        return NotifyPlacementStatusChangedAsync(placement, FosterPlacementStatus.Completed);
    }

    private Task NotifyPlacementCancelledAsync(FosterPlacement placement)
    {
        return NotifyPlacementStatusChangedAsync(placement, FosterPlacementStatus.Cancelled);
    }

    private async Task NotifyPlacementStatusChangedAsync(FosterPlacement placement, FosterPlacementStatus status)
    {
        var statusText = FormatStatus(status).ToLowerInvariant();
        await NotifyShelterAsync(
            placement.Shelter,
            $"Foster placement {statusText}",
            $"{placement.Dog?.Name ?? "A dog"} foster placement was marked {statusText}.",
            status is FosterPlacementStatus.Completed ? NotificationType.Success : NotificationType.Info,
            "/shelter/foster-placements",
            placement.Id);

        await NotifyCaregiverAsync(
            placement,
            $"Foster placement {statusText}",
            $"{placement.Dog?.Name ?? "A dog"} foster placement was marked {statusText}.");
    }

    private async Task NotifyShelterAsync(Shelter? shelter, string title, string message, NotificationType type, string link, int placementId)
    {
        if (notificationService is null || string.IsNullOrWhiteSpace(shelter?.ApplicationUserId))
        {
            return;
        }

        await notificationService.CreateNotificationAsync(
            shelter.ApplicationUserId,
            title,
            message,
            NotificationCategory.FosterCare,
            type,
            link,
            nameof(FosterPlacement),
            placementId.ToString(),
            TimeSpan.FromMinutes(5));
    }

    private async Task NotifyCaregiverAsync(FosterPlacement placement, string title, string message)
    {
        if (notificationService is null || string.IsNullOrWhiteSpace(placement.FosterCaregiverProfile?.UserId))
        {
            return;
        }

        await notificationService.CreateNotificationAsync(
            placement.FosterCaregiverProfile.UserId,
            title,
            message,
            NotificationCategory.FosterCare,
            NotificationType.Info,
            "/notifications",
            nameof(FosterPlacement),
            placement.Id.ToString(),
            TimeSpan.FromMinutes(5));
    }

    private async Task NotifyAdminsAsync(string title, string message, int placementId)
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
                NotificationCategory.FosterCare,
                NotificationType.Warning,
                "/admin/foster-placements",
                nameof(FosterPlacement),
                placementId.ToString(),
                TimeSpan.FromMinutes(5));
        }
    }

    private Task LogPlacementAsync(string action, FosterPlacement placement, string userId, string description)
    {
        return LogAsync(
            action,
            nameof(FosterPlacement),
            placement.Id,
            userId,
            description,
            $"DogId={placement.DogId};ShelterId={placement.ShelterId};CaregiverId={placement.FosterCaregiverProfileId};Status={placement.Status};Priority={placement.Priority}");
    }

    private Task LogAsync(string action, string entityName, int entityId, string userId, string description, string? additionalData = null)
    {
        return auditLogService?.LogAsync(
            action,
            entityName,
            entityId.ToString(),
            description,
            userId: userId,
            additionalData: additionalData) ?? Task.CompletedTask;
    }

    private static FosterCaregiverProfileDto ToCaregiverDto(FosterCaregiverProfile caregiver)
    {
        return new FosterCaregiverProfileDto(
            caregiver.Id,
            caregiver.DisplayName,
            caregiver.Email,
            caregiver.PhoneNumber,
            caregiver.AddressSummary,
            caregiver.PreferredShelterId,
            caregiver.PreferredShelter?.Name,
            caregiver.ExperienceNotes,
            caregiver.HomeEnvironmentNotes,
            caregiver.Capacity,
            caregiver.ActivePlacementCount,
            caregiver.IsActive,
            caregiver.CreatedAtUtc,
            caregiver.UpdatedAtUtc);
    }

    private static FosterPlacementDto ToListDto(FosterPlacement placement, int? viewerShelterId, bool isAdmin)
    {
        var now = DateTime.UtcNow;
        return new FosterPlacementDto(
            placement.Id,
            placement.DogId,
            placement.Dog?.Name ?? "Unknown dog",
            placement.Dog is null ? "Unknown" : DogBreedFormatter.Format(placement.Dog),
            placement.ShelterId,
            placement.Shelter?.Name ?? "Unknown shelter",
            placement.FosterCaregiverProfileId,
            placement.FosterCaregiverProfile?.DisplayName ?? "Unknown caregiver",
            placement.Status,
            placement.Priority,
            placement.Reason,
            placement.StartDateUtc,
            placement.PlannedEndDateUtc,
            placement.ActualEndDateUtc,
            GetDaysInFosterCare(placement),
            IsEndingSoon(placement, now),
            IsOverdue(placement, now),
            CanApprove(placement, viewerShelterId, isAdmin),
            CanStart(placement, viewerShelterId, isAdmin),
            CanComplete(placement, viewerShelterId, isAdmin),
            CanCancel(placement, viewerShelterId, isAdmin));
    }

    private static FosterPlacementDetailsDto ToDetailsDto(FosterPlacement placement, int? shelterId, bool isAdmin)
    {
        return new FosterPlacementDetailsDto(
            placement.Id,
            placement.DogId,
            placement.Dog?.Name ?? "Unknown dog",
            placement.Dog is null ? "Unknown" : DogBreedFormatter.Format(placement.Dog),
            placement.ShelterId,
            placement.Shelter?.Name ?? "Unknown shelter",
            placement.FosterCaregiverProfileId,
            placement.FosterCaregiverProfile?.DisplayName ?? "Unknown caregiver",
            placement.FosterCaregiverProfile?.Email ?? string.Empty,
            placement.FosterCaregiverProfile?.PhoneNumber,
            placement.Status,
            placement.Priority,
            placement.Reason,
            placement.StartDateUtc,
            placement.PlannedEndDateUtc,
            placement.ActualEndDateUtc,
            placement.CareInstructions,
            placement.MedicalNotesSummary,
            placement.ShelterNotes,
            placement.FosterNotes,
            placement.CompletionNotes,
            GetUserDisplayName(placement.CreatedByUser),
            placement.ApprovedByUser is null ? null : GetUserDisplayName(placement.ApprovedByUser),
            placement.EndedByUser is null ? null : GetUserDisplayName(placement.EndedByUser),
            placement.CreatedAtUtc,
            placement.UpdatedAtUtc,
            placement.Activities.OrderBy(activity => activity.CreatedAtUtc).Select(ToActivityDto).ToList(),
            CanApprove(placement, shelterId, isAdmin),
            CanStart(placement, shelterId, isAdmin),
            CanComplete(placement, shelterId, isAdmin),
            CanCancel(placement, shelterId, isAdmin));
    }

    private static FosterPlacementActivityDto ToActivityDto(FosterPlacementActivity activity)
    {
        return new FosterPlacementActivityDto(
            activity.Id,
            activity.ActivityType,
            activity.Message,
            activity.ActorUser is null ? null : GetUserDisplayName(activity.ActorUser),
            activity.CreatedAtUtc);
    }

    private static DogFosterHistoryItemDto ToHistoryDto(FosterPlacement placement)
    {
        return new DogFosterHistoryItemDto(
            placement.Id,
            placement.Status,
            placement.Priority,
            placement.Reason,
            placement.Shelter?.Name ?? "Unknown shelter",
            placement.FosterCaregiverProfile?.DisplayName ?? "Unknown caregiver",
            placement.StartDateUtc,
            placement.PlannedEndDateUtc,
            placement.ActualEndDateUtc,
            placement.CareInstructions ?? placement.ShelterNotes);
    }

    private static bool CanApprove(FosterPlacement placement, int? shelterId, bool isAdmin)
    {
        return placement.Status == FosterPlacementStatus.Pending && shelterId.HasValue && placement.ShelterId == shelterId.Value;
    }

    private static bool CanStart(FosterPlacement placement, int? shelterId, bool isAdmin)
    {
        return placement.Status == FosterPlacementStatus.Approved && shelterId.HasValue && placement.ShelterId == shelterId.Value;
    }

    private static bool CanComplete(FosterPlacement placement, int? shelterId, bool isAdmin)
    {
        return placement.Status == FosterPlacementStatus.Active && (isAdmin || (shelterId.HasValue && placement.ShelterId == shelterId.Value));
    }

    private static bool CanCancel(FosterPlacement placement, int? shelterId, bool isAdmin)
    {
        return placement.Status is FosterPlacementStatus.Pending or FosterPlacementStatus.Approved
               && (isAdmin || (shelterId.HasValue && placement.ShelterId == shelterId.Value));
    }

    private static bool IsEndingSoon(FosterPlacement placement, DateTime now)
    {
        return placement.Status == FosterPlacementStatus.Active
               && placement.PlannedEndDateUtc is { } end
               && end >= now
               && end <= now.AddDays(7);
    }

    private static bool IsOverdue(FosterPlacement placement, DateTime now)
    {
        return placement.Status == FosterPlacementStatus.Active
               && placement.PlannedEndDateUtc is { } end
               && end < now;
    }

    private static int GetDaysInFosterCare(FosterPlacement placement)
    {
        if (placement.Status is FosterPlacementStatus.Pending or FosterPlacementStatus.Approved)
        {
            return 0;
        }

        var end = placement.ActualEndDateUtc ?? DateTime.UtcNow;
        return Math.Max(0, (int)Math.Ceiling((end - placement.StartDateUtc).TotalDays));
    }

    private static void ValidateDateRange(DateTime startDateUtc, DateTime? plannedEndDateUtc)
    {
        if (plannedEndDateUtc.HasValue && plannedEndDateUtc.Value <= startDateUtc)
        {
            throw new InvalidOperationException("Planned end date must be after the start date.");
        }
    }

    private static int ValidateCapacity(int capacity)
    {
        if (capacity < 1)
        {
            throw new InvalidOperationException("Caregiver capacity must be at least 1.");
        }

        return Math.Min(capacity, 20);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        return value.HasValue ? NormalizeUtc(value.Value) : null;
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

    private static string FormatStatus(FosterPlacementStatus status)
    {
        return status.ToString();
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
