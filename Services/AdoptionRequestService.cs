using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class AdoptionRequestService(
    ApplicationDbContext context,
    IEmailService emailService,
    IPdfReportService pdfReportService,
    ILogger<AdoptionRequestService> logger,
    UserManager<ApplicationUser> userManager,
    INotificationService? notificationService = null,
    IAuditLogService? auditLogService = null,
    IReportHistoryService? reportHistoryService = null,
    IDogSearchEmbeddingService? dogSearchEmbeddingService = null,
    IDbContextFactory<ApplicationDbContext>? contextFactory = null) : IAdoptionRequestService
{
    private async Task<T> ExecuteReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        if (contextFactory is null)
        {
            return await query(context);
        }

        await using var readContext = await contextFactory.CreateDbContextAsync();
        return await query(readContext);
    }

    public Task<List<AdoptionRequest>> GetAllAsync()
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.DogBreed)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.SecondaryBreed)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Include(r => r.VisitConfirmedByUser)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<AdoptionRequest?> GetByIdAsync(int id)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.DogBreed)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.SecondaryBreed)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Include(r => r.VisitConfirmedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task CreateAsync(AdoptionRequest adoptionRequest)
    {
        context.AdoptionRequests.Add(adoptionRequest);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AdoptionRequest adoptionRequest)
    {
        adoptionRequest.UpdatedAt = DateTime.UtcNow;
        context.AdoptionRequests.Update(adoptionRequest);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var adoptionRequest = await context.AdoptionRequests.FindAsync(id);
        if (adoptionRequest is null)
        {
            return;
        }

        context.AdoptionRequests.Remove(adoptionRequest);
        await context.SaveChangesAsync();
    }

    public Task<List<AdoptionRequest>> GetForAdopterAsync(string userId)
    {
        return GetRequestsForAdopterAsync(userId);
    }

    public async Task CreateRequestAsync(string adopterId, int dogId, AdoptionRequestQuestionnaire questionnaire)
    {
        await EnsureAdopterAsync(adopterId);
        ValidateQuestionnaire(questionnaire);

        var dog = await context.Dogs
            .Include(d => d.Shelter)
            .ThenInclude(s => s!.ApplicationUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dogId);

        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found.");
        }

        if (dog.Status is not (DogStatus.Available or DogStatus.Reserved))
        {
            throw new InvalidOperationException("Adoption requests can only be submitted for available or reserved dogs.");
        }

        if (await HasPendingRequestAsync(adopterId, dogId))
        {
            throw new InvalidOperationException("You already have a pending request for this dog.");
        }

        VisitSchedulingHelper.ValidatePreferredVisitTime(dog.Shelter, questionnaire.PreferredVisitDateTime);

        var adopter = await context.Users
            .Include(u => u.AdopterProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == adopterId);

        var now = DateTime.UtcNow;
        var adoptionRequest = new AdoptionRequest
        {
            AdopterId = adopterId,
            DogId = dogId,
            Message = string.IsNullOrWhiteSpace(questionnaire.AdditionalInformation) ? null : questionnaire.AdditionalInformation.Trim(),
            ReasonForAdoption = questionnaire.ReasonForAdoption.Trim(),
            HoursAlonePerDay = questionnaire.HoursAlonePerDay,
            AdditionalInformation = string.IsNullOrWhiteSpace(questionnaire.AdditionalInformation) ? null : questionnaire.AdditionalInformation.Trim(),
            Status = AdoptionRequestStatus.Pending,
            PreferredVisitDateTime = questionnaire.PreferredVisitDateTime,
            VisitStatus = AdoptionVisitStatus.Requested,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.AdoptionRequests.Add(adoptionRequest);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.AdoptionRequestSubmitted,
            "AdoptionRequest",
            adoptionRequest.Id.ToString(),
            $"Adoption request for dog {dog.Name} was submitted.",
            userId: adopterId,
            additionalData: $"DogId={dogId};ShelterId={dog.ShelterId};PreferredVisit={questionnaire.PreferredVisitDateTime:O}");
        await NotifyShelterAboutNewRequestAsync(adoptionRequest.Id, dog, adopter, questionnaire, now);
        if (dog.Shelter?.ApplicationUserId is not null)
        {
            await CreateNotificationAsync(
                dog.Shelter.ApplicationUserId,
                "New adoption request",
                $"A new adoption request was submitted for {dog.Name} with a preferred shelter visit time.",
                NotificationCategory.Adoption,
                NotificationType.Info,
                "/shelter/adoption-requests",
                "AdoptionRequest",
                adoptionRequest.Id.ToString());
        }
    }

    public Task<List<AdoptionRequest>> GetRequestsForAdopterAsync(string adopterId)
    {
        return ExecuteReadAsync(db => db.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.DogBreed)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.SecondaryBreed)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Where(r => r.AdopterId == adopterId)
            .OrderByDescending(r => r.CreatedAt)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync());
    }

    public async Task<AdoptionRequestSummary> GetAdoptionRequestSummaryForUserAsync(string adopterId)
    {
        var requests = await context.AdoptionRequests
            .Where(r => r.AdopterId == adopterId)
            .Select(r => r.Status)
            .ToListAsync();

        return new AdoptionRequestSummary(
            requests.Count,
            requests.Count(s => s == AdoptionRequestStatus.Pending),
            requests.Count(s => s == AdoptionRequestStatus.Accepted));
    }

    public Task<List<AdoptionRequest>> GetRecentRequestsForAdopterAsync(string adopterId, int count)
    {
        return ExecuteReadAsync(db => db.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.DogBreed)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.SecondaryBreed)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Where(r => r.AdopterId == adopterId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync());
    }

    public Task<List<AdoptionRequest>> GetRequestsForShelterAsync(int shelterId)
    {
        return ExecuteReadAsync(db => db.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.DogBreed)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.SecondaryBreed)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Where(r => r.Dog != null && r.Dog.ShelterId == shelterId)
            .OrderByDescending(r => r.CreatedAt)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync());
    }

    public async Task<ShelterAdoptionPipelineDto> GetShelterPipelineAsync(string shelterUserId)
    {
        var shelter = await ExecuteReadAsync(db => db.Shelters
            .Where(s => s.ApplicationUserId == shelterUserId)
            .Select(s => new { s.Id, s.Name })
            .AsNoTracking()
            .FirstOrDefaultAsync());

        if (shelter is null)
        {
            throw new InvalidOperationException("No shelter profile is linked to this account.");
        }

        var requests = await ExecuteReadAsync(db => db.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.DogBreed)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.SecondaryBreed)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Where(r => r.Dog != null && r.Dog.ShelterId == shelter.Id)
            .OrderByDescending(r => r.UpdatedAt)
            .ThenByDescending(r => r.CreatedAt)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync());

        return BuildPipelineDto(shelter.Id, shelter.Name, requests);
    }

    public Task<bool> HasPendingRequestAsync(string adopterId, int dogId)
    {
        return context.AdoptionRequests.AnyAsync(r =>
            r.AdopterId == adopterId &&
            r.DogId == dogId &&
            (r.Status == AdoptionRequestStatus.Pending || r.Status == AdoptionRequestStatus.VisitConfirmed));
    }

    public async Task AcceptRequestAsync(int requestId, int shelterId, string? changedByUserId = null)
    {
        await MarkAsAdoptedAsync(requestId, shelterId, changedByUserId);
    }

    public async Task ConfirmVisitAsync(int requestId, int shelterId, string? changedByUserId = null, int? availabilitySlotId = null)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        EnsureShelterCanManageRequest(request, shelterId);
        EnsurePending(request!, "confirmed for a visit");

        if (request!.Dog!.Status == DogStatus.Adopted)
        {
            throw new InvalidOperationException("This dog has already been adopted.");
        }

        if (request.Dog.Status is not (DogStatus.Available or DogStatus.Reserved))
        {
            throw new InvalidOperationException("Only requests for available or reserved dogs can have visits confirmed.");
        }

        if (availabilitySlotId.HasValue)
        {
            await BookAvailabilitySlotForConfirmationAsync(request, shelterId, availabilitySlotId.Value);
        }

        VisitSchedulingHelper.ValidatePreferredVisitTime(request.Dog.Shelter, request.PreferredVisitDateTime);

        var now = DateTime.UtcNow;
        request.Status = AdoptionRequestStatus.VisitConfirmed;
        request.VisitStatus = AdoptionVisitStatus.Confirmed;
        request.VisitConfirmedAt = now;
        request.VisitConfirmedByUserId = changedByUserId;
        request.UpdatedAt = now;
        var oldDogStatus = request.Dog!.Status;
        request.Dog!.Status = DogStatus.Reserved;
        AddDogStatusHistoryIfChanged(
            request.Dog.Id,
            oldDogStatus,
            request.Dog.Status,
            changedByUserId,
            "Status changed to reserved after adoption visit confirmation.");

        var otherPendingRequests = await context.AdoptionRequests
            .Where(r => r.Id != request.Id && r.DogId == request.DogId && r.Status == AdoptionRequestStatus.Pending)
            .ToListAsync();

        foreach (var otherRequest in otherPendingRequests)
        {
            otherRequest.Status = AdoptionRequestStatus.Rejected;
            otherRequest.UpdatedAt = now;
        }

        await ReleaseAvailabilitySlotsForRequestsAsync(otherPendingRequests.Select(r => r.Id));

        await context.SaveChangesAsync();
        await RefreshDogSearchEmbeddingBestEffortAsync(request.Dog.Id);
        await LogAsync(
            AuditActions.VisitConfirmed,
            "AdoptionRequest",
            request.Id.ToString(),
            $"Shelter visit for dog {request.Dog.Name} was confirmed.",
            userId: changedByUserId,
            additionalData: $"DogId={request.DogId};ShelterId={shelterId}");

        if (oldDogStatus != request.Dog.Status)
        {
            await LogAsync(
                AuditActions.DogStatusChanged,
                "Dog",
                request.Dog.Id.ToString(),
                $"Dog {request.Dog.Name} status changed from {oldDogStatus} to {request.Dog.Status} after visit confirmation.",
                userId: changedByUserId,
                additionalData: $"RequestId={request.Id};ShelterId={shelterId}");
        }
        await NotifyAdopterAboutVisitConfirmedAsync(request);
        await CreateNotificationAsync(
            request.AdopterId,
            "Shelter visit confirmed",
            $"Your visit for {request.Dog.Name} has been confirmed.",
            NotificationCategory.Adoption,
            NotificationType.Success,
            "/my-adoption-requests",
            "AdoptionRequest",
            request.Id.ToString());
    }

    public async Task ConfirmPipelineVisitAsync(int requestId, string shelterUserId, int? availabilitySlotId = null)
    {
        var shelterId = await GetShelterIdForUserAsync(shelterUserId);
        await ConfirmVisitAsync(requestId, shelterId, shelterUserId, availabilitySlotId);
    }

    public async Task MarkAsAdoptedAsync(int requestId, int shelterId, string? changedByUserId = null)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        EnsureShelterCanManageRequest(request, shelterId);
        if (request!.Status != AdoptionRequestStatus.VisitConfirmed || request.VisitStatus != AdoptionVisitStatus.Confirmed)
        {
            throw new InvalidOperationException("Only requests with a confirmed visit can be marked as adopted.");
        }

        if (request.Dog!.Status == DogStatus.Adopted)
        {
            throw new InvalidOperationException("This dog has already been adopted.");
        }

        var now = DateTime.UtcNow;
        request.Status = AdoptionRequestStatus.Accepted;
        request.VisitStatus = AdoptionVisitStatus.Completed;
        request.UpdatedAt = now;
        var oldDogStatus = request.Dog.Status;
        request.Dog.Status = DogStatus.Adopted;
        request.Dog.AdoptedAt ??= now;
        AddDogStatusHistoryIfChanged(
            request.Dog.Id,
            oldDogStatus,
            request.Dog.Status,
            changedByUserId,
            "Status changed to adopted after confirmed shelter visit.");

        var otherActiveRequests = await context.AdoptionRequests
            .Where(r =>
                r.Id != request.Id &&
                r.DogId == request.DogId &&
                (r.Status == AdoptionRequestStatus.Pending || r.Status == AdoptionRequestStatus.VisitConfirmed))
            .ToListAsync();

        foreach (var otherRequest in otherActiveRequests)
        {
            otherRequest.Status = AdoptionRequestStatus.Rejected;
            otherRequest.VisitStatus = AdoptionVisitStatus.Cancelled;
            otherRequest.UpdatedAt = now;
        }

        await ReleaseAvailabilitySlotsForRequestsAsync(otherActiveRequests.Select(r => r.Id));

        await context.SaveChangesAsync();
        await RefreshDogSearchEmbeddingBestEffortAsync(request.Dog.Id);
        await LogAsync(
            AuditActions.VisitCompleted,
            "AdoptionRequest",
            request.Id.ToString(),
            $"Visit for dog {request.Dog.Name} was completed.",
            userId: changedByUserId,
            additionalData: $"DogId={request.DogId};ShelterId={shelterId}");
        await LogAsync(
            AuditActions.DogMarkedAdopted,
            "Dog",
            request.Dog.Id.ToString(),
            $"Dog {request.Dog.Name} was marked as adopted after a confirmed visit.",
            userId: changedByUserId,
            additionalData: $"RequestId={request.Id};ShelterId={shelterId}");
        await LogAsync(
            AuditActions.AdoptionRequestAccepted,
            "AdoptionRequest",
            request.Id.ToString(),
            $"Adoption request for dog {request.Dog.Name} was accepted after the visit.",
            userId: changedByUserId,
            additionalData: $"DogId={request.DogId};ShelterId={shelterId}");
        await NotifyAdopterAboutRequestStatusAsync(request, AdoptionRequestStatus.Accepted);
        await CreateNotificationAsync(
            request.AdopterId,
            "Adoption finalized",
            $"Your adoption request for {request.Dog.Name} was accepted after the visit.",
            NotificationCategory.Adoption,
            NotificationType.Success,
            "/my-adoption-requests",
            "AdoptionRequest",
            request.Id.ToString());
    }

    public async Task MarkPipelineRequestAsAdoptedAsync(int requestId, string shelterUserId)
    {
        var shelterId = await GetShelterIdForUserAsync(shelterUserId);
        await MarkAsAdoptedAsync(requestId, shelterId, shelterUserId);
    }

    public async Task RejectRequestAsync(int requestId, int shelterId)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        EnsureShelterCanManageRequest(request, shelterId);
        EnsureCanReject(request!);

        var wasConfirmedVisit = request!.Status == AdoptionRequestStatus.VisitConfirmed;
        var oldDogStatus = request.Dog?.Status;
        request!.Status = AdoptionRequestStatus.Rejected;
        request.VisitStatus = request.VisitStatus == AdoptionVisitStatus.NotScheduled
            ? AdoptionVisitStatus.NotScheduled
            : AdoptionVisitStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;

        await ReleaseAvailabilitySlotsForRequestsAsync([request.Id]);

        if (wasConfirmedVisit && request.Dog is not null && request.Dog.Status == DogStatus.Reserved)
        {
            request.Dog.Status = DogStatus.Available;
            AddDogStatusHistoryIfChanged(
                request.Dog.Id,
                oldDogStatus!.Value,
                DogStatus.Available,
                null,
                "Dog returned to available after a confirmed visit was rejected.");
        }

        await context.SaveChangesAsync();
        if (request.Dog is not null)
        {
            await RefreshDogSearchEmbeddingBestEffortAsync(request.Dog.Id);
        }
        await LogAsync(
            AuditActions.AdoptionRequestRejected,
            "AdoptionRequest",
            request.Id.ToString(),
            $"Adoption request for dog {request.Dog?.Name ?? "Unknown dog"} was rejected.",
            additionalData: $"DogId={request.DogId};ShelterId={shelterId}");
        await NotifyAdopterAboutRequestStatusAsync(request, AdoptionRequestStatus.Rejected);
        await CreateNotificationAsync(
            request.AdopterId,
            "Adoption request rejected",
            $"Your request for {request.Dog?.Name ?? "the selected dog"} was rejected.",
            NotificationCategory.Adoption,
            NotificationType.Warning,
            "/my-adoption-requests",
            "AdoptionRequest",
            request.Id.ToString());
    }

    public async Task RejectPipelineRequestAsync(int requestId, string shelterUserId)
    {
        var shelterId = await GetShelterIdForUserAsync(shelterUserId);
        await RejectRequestAsync(requestId, shelterId);
    }

    public async Task CancelRequestAsync(int requestId, string adopterId)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
        {
            throw new InvalidOperationException("Adoption request was not found.");
        }

        if (request.AdopterId != adopterId)
        {
            throw new InvalidOperationException("You can only cancel your own adoption requests.");
        }

        EnsurePending(request, "cancelled");

        request.Status = AdoptionRequestStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;

        await ReleaseAvailabilitySlotsForRequestsAsync([request.Id]);

        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.AdoptionRequestCancelled,
            "AdoptionRequest",
            request.Id.ToString(),
            "Adoption request was cancelled by the adopter.",
            userId: adopterId,
            additionalData: $"DogId={request.DogId}");
        if (request.Dog?.Shelter?.ApplicationUserId is not null)
        {
            await CreateNotificationAsync(
                request.Dog.Shelter.ApplicationUserId,
                "Adoption request cancelled",
                $"An adopter cancelled a request for {request.Dog.Name}.",
                NotificationCategory.Adoption,
                NotificationType.Info,
                "/shelter/adoption-requests",
                "AdoptionRequest",
                request.Id.ToString());
        }
    }

    public async Task UpdateShelterInternalNotesAsync(int requestId, int shelterId, string? notes)
    {
        if (!string.IsNullOrWhiteSpace(notes) && notes.Length > 2000)
        {
            throw new InvalidOperationException("Internal notes must be 2000 characters or fewer.");
        }

        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        EnsureShelterCanManageRequest(request, shelterId);

        request!.ShelterInternalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        request.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    private static void EnsureShelterCanManageRequest(AdoptionRequest? request, int shelterId)
    {
        if (request?.Dog is null || request.Dog.ShelterId != shelterId)
        {
            throw new InvalidOperationException("You cannot manage requests for another shelter's dog.");
        }
    }

    private async Task<int> GetShelterIdForUserAsync(string shelterUserId)
    {
        if (string.IsNullOrWhiteSpace(shelterUserId))
        {
            throw new InvalidOperationException("Current shelter account could not be found.");
        }

        var shelterId = await context.Shelters
            .Where(shelter => shelter.ApplicationUserId == shelterUserId)
            .Select(shelter => (int?)shelter.Id)
            .FirstOrDefaultAsync();

        return shelterId ?? throw new InvalidOperationException("No shelter profile is linked to this account.");
    }

    private async Task BookAvailabilitySlotForConfirmationAsync(
        AdoptionRequest request,
        int shelterId,
        int availabilitySlotId)
    {
        var slot = await context.ShelterAvailabilitySlots
            .FirstOrDefaultAsync(candidate => candidate.Id == availabilitySlotId);

        if (slot is null)
        {
            throw new InvalidOperationException("Availability slot was not found.");
        }

        if (slot.ShelterId != shelterId || slot.ShelterId != request.Dog?.ShelterId)
        {
            throw new InvalidOperationException("This slot belongs to another shelter.");
        }

        if (slot.IsCancelled)
        {
            throw new InvalidOperationException("Cancelled slots cannot be booked.");
        }

        if (slot.StartTime <= DateTime.Now)
        {
            throw new InvalidOperationException("Past slots cannot be booked.");
        }

        if (slot.IsBooked && slot.BookedAdoptionRequestId != request.Id)
        {
            throw new InvalidOperationException("This slot is already booked.");
        }

        var existingSlots = await context.ShelterAvailabilitySlots
            .Where(candidate => candidate.BookedAdoptionRequestId == request.Id && candidate.Id != slot.Id)
            .ToListAsync();

        foreach (var existingSlot in existingSlots)
        {
            ReleaseSlot(existingSlot);
        }

        slot.IsBooked = true;
        slot.BookedAdoptionRequestId = request.Id;
        request.PreferredVisitDateTime = slot.StartTime;
    }

    private async Task ReleaseAvailabilitySlotsForRequestsAsync(IEnumerable<int> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var slots = await context.ShelterAvailabilitySlots
            .Where(slot => slot.BookedAdoptionRequestId.HasValue && ids.Contains(slot.BookedAdoptionRequestId.Value))
            .ToListAsync();

        foreach (var slot in slots)
        {
            ReleaseSlot(slot);
        }
    }

    private static void ReleaseSlot(ShelterAvailabilitySlot slot)
    {
        slot.IsBooked = false;
        slot.BookedAdoptionRequestId = null;
    }

    private static ShelterAdoptionPipelineDto BuildPipelineDto(
        int shelterId,
        string shelterName,
        IReadOnlyList<AdoptionRequest> requests)
    {
        var cards = requests.Select(ToPipelineCardDto).ToList();

        return new ShelterAdoptionPipelineDto(
            shelterId,
            shelterName,
            cards.Count,
            [
                BuildColumn(AdoptionPipelineStage.Pending, "Pending", "New requests waiting for shelter review.", cards),
                BuildColumn(AdoptionPipelineStage.VisitConfirmed, "Visit Confirmed", "Requests with a confirmed shelter visit.", cards),
                BuildColumn(AdoptionPipelineStage.Accepted, "Accepted", "Completed adoption requests.", cards),
                BuildColumn(AdoptionPipelineStage.Closed, "Rejected / Cancelled", "Requests that are no longer active.", cards)
            ]);
    }

    private static AdoptionPipelineColumnDto BuildColumn(
        AdoptionPipelineStage stage,
        string title,
        string description,
        IReadOnlyList<AdoptionPipelineCardDto> cards)
    {
        return new AdoptionPipelineColumnDto(
            stage,
            title,
            description,
            cards.Where(card => GetPipelineStage(card.RequestStatus) == stage).ToList());
    }

    private static AdoptionPipelineCardDto ToPipelineCardDto(AdoptionRequest request)
    {
        var dog = request.Dog;
        return new AdoptionPipelineCardDto(
            request.Id,
            request.DogId,
            string.IsNullOrWhiteSpace(dog?.Name) ? "Unknown dog" : dog.Name,
            DogBreedFormatter.Format(dog),
            dog is null ? null : DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images),
            dog?.Status ?? DogStatus.Available,
            GetAdopterDisplayName(request.Adopter),
            request.Adopter?.AdopterProfile?.City,
            request.Status,
            request.VisitStatus,
            request.PreferredVisitDateTime,
            request.CreatedAt,
            request.UpdatedAt,
            BuildQuestionnairePreview(request),
            CanConfirmVisitFromPipeline(request),
            request.Status is AdoptionRequestStatus.Pending or AdoptionRequestStatus.VisitConfirmed,
            request.Status == AdoptionRequestStatus.VisitConfirmed &&
                request.VisitStatus == AdoptionVisitStatus.Confirmed &&
                dog?.Status != DogStatus.Adopted,
            false);
    }

    private static AdoptionPipelineStage GetPipelineStage(AdoptionRequestStatus status)
    {
        return status switch
        {
            AdoptionRequestStatus.Pending => AdoptionPipelineStage.Pending,
            AdoptionRequestStatus.VisitConfirmed => AdoptionPipelineStage.VisitConfirmed,
            AdoptionRequestStatus.Accepted => AdoptionPipelineStage.Accepted,
            AdoptionRequestStatus.Rejected or AdoptionRequestStatus.Cancelled => AdoptionPipelineStage.Closed,
            _ => AdoptionPipelineStage.Closed
        };
    }

    private static bool CanConfirmVisitFromPipeline(AdoptionRequest request)
    {
        return request.Status == AdoptionRequestStatus.Pending &&
            request.PreferredVisitDateTime.HasValue &&
            request.PreferredVisitDateTime.Value > DateTime.Now &&
            request.Dog?.Status is DogStatus.Available or DogStatus.Reserved;
    }

    private static string GetAdopterDisplayName(ApplicationUser? adopter)
    {
        if (adopter is null)
        {
            return "Unknown adopter";
        }

        return string.IsNullOrWhiteSpace(adopter.FullName)
            ? adopter.Email ?? adopter.UserName ?? "Unknown adopter"
            : adopter.FullName;
    }

    private static string? BuildQuestionnairePreview(AdoptionRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.ReasonForAdoption))
        {
            parts.Add(request.ReasonForAdoption.Trim());
        }

        if (request.HoursAlonePerDay.HasValue)
        {
            parts.Add($"{request.HoursAlonePerDay.Value}h alone/day");
        }

        if (!string.IsNullOrWhiteSpace(request.AdditionalInformation))
        {
            parts.Add(request.AdditionalInformation.Trim());
        }

        var preview = string.Join(" · ", parts);
        return string.IsNullOrWhiteSpace(preview)
            ? null
            : preview.Length <= 160 ? preview : $"{preview[..160]}...";
    }

    private static void EnsurePending(AdoptionRequest request, string action)
    {
        if (request.Status != AdoptionRequestStatus.Pending)
        {
            throw new InvalidOperationException($"Only pending requests can be {action}.");
        }
    }

    private static void EnsureCanReject(AdoptionRequest request)
    {
        if (request.Status is not (AdoptionRequestStatus.Pending or AdoptionRequestStatus.VisitConfirmed))
        {
            throw new InvalidOperationException("Only pending or visit-confirmed requests can be rejected.");
        }
    }

    private static void ValidateQuestionnaire(AdoptionRequestQuestionnaire questionnaire)
    {
        if (string.IsNullOrWhiteSpace(questionnaire.ReasonForAdoption))
        {
            throw new InvalidOperationException("Reason for adoption is required.");
        }

        if (questionnaire.HoursAlonePerDay is < 0 or > 24)
        {
            throw new InvalidOperationException("Hours alone per day must be between 0 and 24.");
        }
    }

    private async Task EnsureAdopterAsync(string adopterId)
    {
        var user = await userManager.FindByIdAsync(adopterId);
        if (user is null || !await userManager.IsInRoleAsync(user, IdentitySeedData.AdopterRole))
        {
            throw new InvalidOperationException("Only adopter accounts can submit adoption requests.");
        }
    }

    private void AddDogStatusHistoryIfChanged(int dogId, DogStatus oldStatus, DogStatus newStatus, string? changedByUserId, string notes)
    {
        if (oldStatus == newStatus)
        {
            return;
        }

        context.DogStatusHistories.Add(new DogStatusHistory
        {
            DogId = dogId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = changedByUserId,
            Notes = notes
        });
    }

    private async Task NotifyShelterAboutNewRequestAsync(int adoptionRequestId, Dog dog, PawConnect.Data.ApplicationUser? adopter, AdoptionRequestQuestionnaire questionnaire, DateTime createdAt)
    {
        try
        {
            var shelterEmail = dog.Shelter?.ApplicationUser?.Email ?? dog.Shelter?.Email;
            var adopterName = string.IsNullOrWhiteSpace(adopter?.FullName)
                ? adopter?.Email ?? "An adopter"
                : $"{adopter.FullName} ({adopter.Email})";

            var body = $"""
                Hello,

                A new adoption request was submitted for {dog.Name}.

                Adopter: {adopterName}
                Created: {createdAt.ToLocalTime():dd MMM yyyy HH:mm}
                Preferred visit: {VisitSchedulingHelper.FormatVisitDateTime(questionnaire.PreferredVisitDateTime)}

                Reason for adoption:
                {questionnaire.ReasonForAdoption.Trim()}

                Hours alone per day: {(questionnaire.HoursAlonePerDay.HasValue ? questionnaire.HoursAlonePerDay.Value.ToString() : "Not provided")}
                Additional information: {(string.IsNullOrWhiteSpace(questionnaire.AdditionalInformation) ? "Not provided." : questionnaire.AdditionalInformation.Trim())}

                Adopter profile summary:
                {BuildAdopterProfileSummary(adopter?.AdopterProfile)}

                Please review the request in PawConnect.
                """;

            var attachments = await TryCreatePdfAttachmentAsync(
                "AdoptionRequestReport.pdf",
                () => pdfReportService.GenerateAdoptionRequestReportAsync(adoptionRequestId));

            var htmlBody = PawConnectEmailTemplate.BuildHtml(
                $"New adoption request for {dog.Name}",
                "Hello,",
                [
                    "A new adoption request was submitted in PawConnect.",
                    $"Reason for adoption: {questionnaire.ReasonForAdoption.Trim()}",
                    string.IsNullOrWhiteSpace(questionnaire.AdditionalInformation)
                        ? "Additional information: Not provided."
                        : $"Additional information: {questionnaire.AdditionalInformation.Trim()}",
                    "Please review the request in PawConnect."
                ],
                details:
                [
                    new("Dog", dog.Name),
                    new("Shelter", dog.Shelter?.Name ?? "Your shelter"),
                    new("Adopter", adopterName),
                    new("Request status", AdoptionRequestStatus.Pending.ToString()),
                    new("Preferred visit", VisitSchedulingHelper.FormatVisitDateTime(questionnaire.PreferredVisitDateTime)),
                    new("Created", createdAt.ToLocalTime().ToString("dd MMM yyyy HH:mm")),
                    new("Hours alone per day", questionnaire.HoursAlonePerDay.HasValue ? questionnaire.HoursAlonePerDay.Value.ToString() : "Not provided")
                ],
                hasAttachment: attachments.Count > 0);

            var subject = $"New adoption request for {dog.Name}";
            await emailService.SendEmailAsync(shelterEmail ?? string.Empty, subject, body, attachments, htmlBody);
            await RecordPdfEmailReportAsync(
                ReportHistoryTypes.AdoptionRequestReport,
                shelterEmail,
                subject,
                attachments,
                ReportHistoryTriggers.System,
                dog.ShelterId,
                "AdoptionRequest",
                adoptionRequestId.ToString());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shelter adoption request notification failed for dog {DogId}.", dog.Id);
        }
    }

    private async Task NotifyAdopterAboutVisitConfirmedAsync(AdoptionRequest request)
    {
        try
        {
            var adopterEmail = request.Adopter?.Email;
            var dogName = request.Dog?.Name ?? "the selected dog";
            var shelter = request.Dog?.Shelter;
            var shelterName = shelter?.Name ?? "the shelter";
            var visitTime = VisitSchedulingHelper.FormatVisitDateTime(request.PreferredVisitDateTime);
            var shelterAddress = string.Join(", ", new[] { shelter?.Address, shelter?.City }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            var attachments = new List<EmailAttachment>
            {
                VisitSchedulingHelper.CreateCalendarInviteAttachment(request)
            };

            var body = $"""
                Hello,

                Your PawConnect shelter visit has been confirmed.

                Dog: {dogName}
                Shelter: {shelterName}
                Visit time: {visitTime}
                Shelter address: {(string.IsNullOrWhiteSpace(shelterAddress) ? "Not provided" : shelterAddress)}
                Shelter email: {(string.IsNullOrWhiteSpace(shelter?.Email) ? "Not provided" : shelter.Email)}
                Shelter phone: {(string.IsNullOrWhiteSpace(shelter?.PhoneNumber) ? "Not provided" : shelter.PhoneNumber)}

                A calendar invitation is included in this email.
                If you cannot attend, please contact the shelter.
                """;

            var htmlBody = PawConnectEmailTemplate.BuildHtml(
                "Your PawConnect shelter visit has been confirmed",
                "Hello,",
                [
                    $"Your visit for {dogName} has been confirmed.",
                    "A calendar invitation is included in this email.",
                    "If you cannot attend, please contact the shelter."
                ],
                details:
                [
                    new("Dog", dogName),
                    new("Shelter", shelterName),
                    new("Visit time", visitTime),
                    new("Shelter address", string.IsNullOrWhiteSpace(shelterAddress) ? "Not provided" : shelterAddress),
                    new("Shelter email", shelter?.Email ?? "Not provided"),
                    new("Shelter phone", shelter?.PhoneNumber ?? "Not provided")
                ]);

            await emailService.SendEmailAsync(adopterEmail ?? string.Empty, "Your PawConnect shelter visit has been confirmed", body, attachments, htmlBody);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Visit confirmation email failed for request {RequestId}.", request.Id);
        }
    }

    private static string BuildAdopterProfileSummary(AdopterProfile? profile)
    {
        if (profile is null)
        {
            return "No adopter profile completed yet.";
        }

        return $"""
            City: {profile.City}
            Housing: {profile.HousingType}
            Has yard: {FormatYesNo(profile.HasYard)}
            Has other pets: {FormatYesNo(profile.HasOtherPets)}
            Has children: {FormatYesNo(profile.HasChildren)}
            Experience with dogs: {(string.IsNullOrWhiteSpace(profile.ExperienceWithDogs) ? "Not provided." : profile.ExperienceWithDogs)}
            """;
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private async Task NotifyAdopterAboutRequestStatusAsync(AdoptionRequest request, AdoptionRequestStatus status)
    {
        try
        {
            var adopterEmail = request.Adopter?.Email;
            var dogName = request.Dog?.Name ?? "the selected dog";
            var shelterName = request.Dog?.Shelter?.Name ?? "the shelter";

            var body = $"""
                Hello,

                Your adoption request for {dogName} has been {status.ToString().ToLowerInvariant()}.

                Shelter: {shelterName}
                Status: {status}

                Thank you for using PawConnect.
                """;

            var attachments = await TryCreatePdfAttachmentAsync(
                "AdoptionStatusReport.pdf",
                () => pdfReportService.GenerateAdoptionStatusReportAsync(request.Id));

            var htmlBody = PawConnectEmailTemplate.BuildHtml(
                $"Adoption request {status.ToString().ToLowerInvariant()}",
                "Hello,",
                [
                    $"Your adoption request for {dogName} has been {status.ToString().ToLowerInvariant()}.",
                    "Thank you for using PawConnect."
                ],
                details:
                [
                    new("Dog", dogName),
                    new("Shelter", shelterName),
                    new("Request status", status.ToString())
                ],
                hasAttachment: attachments.Count > 0);

            var subject = $"Adoption request {status}: {dogName}";
            await emailService.SendEmailAsync(adopterEmail ?? string.Empty, subject, body, attachments, htmlBody);
            await RecordPdfEmailReportAsync(
                ReportHistoryTypes.AdoptionStatusReport,
                adopterEmail,
                subject,
                attachments,
                ReportHistoryTriggers.Shelter,
                request.Dog?.ShelterId,
                "AdoptionRequest",
                request.Id.ToString());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Adopter adoption request notification failed for request {RequestId}.", request.Id);
        }
    }

    private async Task<List<EmailAttachment>> TryCreatePdfAttachmentAsync(string fileName, Func<Task<byte[]>> generatePdf)
    {
        try
        {
            var pdfBytes = await generatePdf();
            return
            [
                new EmailAttachment
                {
                    FileName = fileName,
                    ContentType = "application/pdf",
                    Content = pdfBytes
                }
            ];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PDF attachment {FileName} could not be generated.", fileName);
            return [];
        }
    }

    private Task RecordPdfEmailReportAsync(
        string reportType,
        string? recipientEmail,
        string subject,
        IReadOnlyList<EmailAttachment> attachments,
        string triggeredBy,
        int? shelterId,
        string relatedEntityName,
        string relatedEntityId)
    {
        if (reportHistoryService is null || attachments.Count == 0)
        {
            return Task.CompletedTask;
        }

        return reportHistoryService.RecordReportSentAsync(new ReportHistoryRecord(
            reportType,
            triggeredBy,
            recipientEmail,
            subject,
            attachments[0].FileName,
            GeneratedAt: DateTime.UtcNow,
            SentAt: DateTime.UtcNow,
            ShelterId: shelterId,
            RelatedEntityName: relatedEntityName,
            RelatedEntityId: relatedEntityId));
    }

    private Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        string? userId = null,
        string? additionalData = null)
    {
        return auditLogService?.LogAsync(action, entityName, entityId, description, userId: userId, additionalData: additionalData) ?? Task.CompletedTask;
    }

    private Task CreateNotificationAsync(
        string userId,
        string title,
        string message,
        NotificationCategory category,
        NotificationType type,
        string link,
        string relatedEntityName,
        string relatedEntityId)
    {
        return notificationService?.CreateNotificationAsync(
            userId,
            title,
            message,
            category,
            type,
            link,
            relatedEntityName,
            relatedEntityId) ?? Task.CompletedTask;
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
            logger.LogWarning(ex, "Dog search embedding refresh failed after adoption request status change for DogId {DogId}.", dogId);
        }
    }
}
