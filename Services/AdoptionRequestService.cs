using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class AdoptionRequestService(ApplicationDbContext context) : IAdoptionRequestService
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
        var dog = await context.Dogs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dogId);
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
    }

    public async Task RejectRequestAsync(int requestId, int shelterId)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        EnsureShelterCanManageRequest(request, shelterId);
        EnsurePending(request!);

        request!.Status = AdoptionRequestStatus.Rejected;
        request.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
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
}
