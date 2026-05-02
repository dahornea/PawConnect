using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class AdoptionRequestService(ApplicationDbContext context, IEmailService emailService, ILogger<AdoptionRequestService> logger) : IAdoptionRequestService
{
    public Task<List<AdoptionRequest>> GetAllAsync()
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
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

    public async Task CreateRequestAsync(string adopterId, int dogId, string? message)
    {
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

        var adopter = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == adopterId);
        var now = DateTime.UtcNow;
        context.AdoptionRequests.Add(new AdoptionRequest
        {
            AdopterId = adopterId,
            DogId = dogId,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            Status = AdoptionRequestStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync();
        await NotifyShelterAboutNewRequestAsync(dog, adopter, message, now);
    }

    public Task<List<AdoptionRequest>> GetRequestsForAdopterAsync(string adopterId)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Where(r => r.AdopterId == adopterId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<AdoptionRequest>> GetRequestsForShelterAsync(int shelterId)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Images)
            .Include(r => r.Adopter)
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

    public async Task AcceptRequestAsync(int requestId, int shelterId)
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
        request.Dog!.Status = DogStatus.Reserved;

        var otherPendingRequests = await context.AdoptionRequests
            .Where(r => r.Id != request.Id && r.DogId == request.DogId && r.Status == AdoptionRequestStatus.Pending)
            .ToListAsync();

        foreach (var otherRequest in otherPendingRequests)
        {
            otherRequest.Status = AdoptionRequestStatus.Rejected;
            otherRequest.UpdatedAt = now;
        }

        await context.SaveChangesAsync();
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

    private async Task NotifyShelterAboutNewRequestAsync(Dog dog, PawConnect.Data.ApplicationUser? adopter, string? message, DateTime createdAt)
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
                Message: {(string.IsNullOrWhiteSpace(message) ? "No message provided." : message.Trim())}

                Please review the request in PawConnect.
                """;

            await emailService.SendEmailAsync(shelterEmail ?? string.Empty, $"New adoption request for {dog.Name}", body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shelter adoption request notification failed for dog {DogId}.", dog.Id);
        }
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

            await emailService.SendEmailAsync(adopterEmail ?? string.Empty, $"Adoption request {status}: {dogName}", body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Adopter adoption request notification failed for request {RequestId}.", request.Id);
        }
    }
}
