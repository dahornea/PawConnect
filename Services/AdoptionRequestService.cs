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
    IAuditLogService? auditLogService = null) : IAdoptionRequestService
{
    public Task<List<AdoptionRequest>> GetAllAsync()
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
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
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
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

        if (dog.Status is DogStatus.Adopted or DogStatus.InTreatment)
        {
            throw new InvalidOperationException("Adoption requests can only be submitted for available or reserved dogs.");
        }

        if (await HasPendingRequestAsync(adopterId, dogId))
        {
            throw new InvalidOperationException("You already have a pending adoption request for this dog.");
        }

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
            additionalData: $"DogId={dogId};ShelterId={dog.ShelterId}");
        await NotifyShelterAboutNewRequestAsync(adoptionRequest.Id, dog, adopter, questionnaire, now);
    }

    public Task<List<AdoptionRequest>> GetRequestsForAdopterAsync(string adopterId)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Where(r => r.AdopterId == adopterId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
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
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Where(r => r.AdopterId == adopterId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<AdoptionRequest>> GetRequestsForShelterAsync(int shelterId)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Where(r => r.Dog != null && r.Dog.ShelterId == shelterId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<bool> HasPendingRequestAsync(string adopterId, int dogId)
    {
        return context.AdoptionRequests.AnyAsync(r =>
            r.AdopterId == adopterId &&
            r.DogId == dogId &&
            r.Status == AdoptionRequestStatus.Pending);
    }

    public async Task AcceptRequestAsync(int requestId, int shelterId, string? changedByUserId = null)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        EnsureShelterCanManageRequest(request, shelterId);
        EnsurePending(request!);

        var now = DateTime.UtcNow;
        request!.Status = AdoptionRequestStatus.Accepted;
        request.UpdatedAt = now;
        var oldDogStatus = request.Dog!.Status;
        request.Dog!.Status = DogStatus.Reserved;
        AddDogStatusHistoryIfChanged(
            request.Dog.Id,
            oldDogStatus,
            request.Dog.Status,
            changedByUserId,
            "Status changed to reserved after adoption request acceptance.");

        var otherPendingRequests = await context.AdoptionRequests
            .Where(r => r.Id != request.Id && r.DogId == request.DogId && r.Status == AdoptionRequestStatus.Pending)
            .ToListAsync();

        foreach (var otherRequest in otherPendingRequests)
        {
            otherRequest.Status = AdoptionRequestStatus.Rejected;
            otherRequest.UpdatedAt = now;
        }

        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.AdoptionRequestAccepted,
            "AdoptionRequest",
            request.Id.ToString(),
            $"Adoption request for dog {request.Dog.Name} was accepted.",
            userId: changedByUserId,
            additionalData: $"DogId={request.DogId};ShelterId={shelterId}");

        if (oldDogStatus != request.Dog.Status)
        {
            await LogAsync(
                AuditActions.DogStatusChanged,
                "Dog",
                request.Dog.Id.ToString(),
                $"Dog {request.Dog.Name} status changed from {oldDogStatus} to {request.Dog.Status} after adoption request acceptance.",
                userId: changedByUserId,
                additionalData: $"RequestId={request.Id};ShelterId={shelterId}");
        }
        await NotifyAdopterAboutRequestStatusAsync(request, AdoptionRequestStatus.Accepted);
    }

    public async Task RejectRequestAsync(int requestId, int shelterId)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        EnsureShelterCanManageRequest(request, shelterId);
        EnsurePending(request!);

        request!.Status = AdoptionRequestStatus.Rejected;
        request.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.AdoptionRequestRejected,
            "AdoptionRequest",
            request.Id.ToString(),
            $"Adoption request for dog {request.Dog?.Name ?? "Unknown dog"} was rejected.",
            additionalData: $"DogId={request.DogId};ShelterId={shelterId}");
        await NotifyAdopterAboutRequestStatusAsync(request, AdoptionRequestStatus.Rejected);
    }

    public async Task CancelRequestAsync(int requestId, string adopterId)
    {
        var request = await context.AdoptionRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null || request.AdopterId != adopterId)
        {
            throw new InvalidOperationException("Adoption request was not found.");
        }

        EnsurePending(request);

        request.Status = AdoptionRequestStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.AdoptionRequestCancelled,
            "AdoptionRequest",
            request.Id.ToString(),
            "Adoption request was cancelled by the adopter.",
            userId: adopterId,
            additionalData: $"DogId={request.DogId}");
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
            throw new InvalidOperationException("This adoption request does not belong to your shelter.");
        }
    }

    private static void EnsurePending(AdoptionRequest request)
    {
        if (request.Status != AdoptionRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending adoption requests can be updated.");
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
                    new("Created", createdAt.ToLocalTime().ToString("dd MMM yyyy HH:mm")),
                    new("Hours alone per day", questionnaire.HoursAlonePerDay.HasValue ? questionnaire.HoursAlonePerDay.Value.ToString() : "Not provided")
                ],
                hasAttachment: attachments.Count > 0);

            await emailService.SendEmailAsync(shelterEmail ?? string.Empty, $"New adoption request for {dog.Name}", body, attachments, htmlBody);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shelter adoption request notification failed for dog {DogId}.", dog.Id);
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

            await emailService.SendEmailAsync(adopterEmail ?? string.Empty, $"Adoption request {status}: {dogName}", body, attachments, htmlBody);
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
}
