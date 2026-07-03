using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ShelterAvailabilityService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IAuditLogService? auditLogService = null) : IShelterAvailabilityService
{
    private static readonly TimeSpan MinimumSlotDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaximumSlotDuration = TimeSpan.FromHours(4);
    private const int MaximumNotesLength = 500;

    public async Task<List<ShelterAvailabilitySlotDto>> GetShelterSlotsAsync(
        int shelterId,
        DateTime from,
        DateTime to,
        string currentUserId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await EnsureShelterOwnershipAsync(context, shelterId, currentUserId);

        var rangeStart = from.Date;
        var rangeEnd = to.Date.AddDays(1);

        var slots = await context.ShelterAvailabilitySlots
            .Include(slot => slot.BookedAdoptionRequest)
            .ThenInclude(request => request!.Dog)
            .Include(slot => slot.BookedAdoptionRequest)
            .ThenInclude(request => request!.Adopter)
            .Where(slot => slot.ShelterId == shelterId &&
                slot.StartTime < rangeEnd &&
                slot.EndTime > rangeStart)
            .OrderBy(slot => slot.StartTime)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();

        return slots.Select(ToDto).ToList();
    }

    public async Task<ShelterAvailabilitySlotDto> CreateSlotAsync(
        CreateShelterAvailabilitySlotRequest request,
        string currentUserId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await EnsureShelterOwnershipAsync(context, request.ShelterId, currentUserId);
        var shelter = await context.Shelters.AsNoTracking().FirstAsync(candidate => candidate.Id == request.ShelterId);
        ValidateSlotInput(shelter, request.StartTime, request.EndTime, request.Notes);

        var hasOverlap = await context.ShelterAvailabilitySlots.AnyAsync(slot =>
            slot.ShelterId == request.ShelterId &&
            !slot.IsCancelled &&
            slot.StartTime < request.EndTime &&
            slot.EndTime > request.StartTime);

        if (hasOverlap)
        {
            throw new InvalidOperationException("This slot overlaps an existing active availability slot.");
        }

        var slot = new ShelterAvailabilitySlot
        {
            ShelterId = request.ShelterId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        context.ShelterAvailabilitySlots.Add(slot);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ShelterAvailabilitySlotCreated,
            "ShelterAvailabilitySlot",
            slot.Id.ToString(),
            $"Availability slot was created for shelter {shelter.Name}.",
            currentUserId,
            $"ShelterId={request.ShelterId};StartTime={request.StartTime:O};EndTime={request.EndTime:O}");

        return ToDto(slot);
    }

    public async Task CancelSlotAsync(int slotId, string currentUserId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var slot = await context.ShelterAvailabilitySlots
            .FirstOrDefaultAsync(candidate => candidate.Id == slotId);

        if (slot is null)
        {
            throw new InvalidOperationException("Availability slot was not found.");
        }

        await EnsureShelterOwnershipAsync(context, slot.ShelterId, currentUserId);

        if (slot.IsCancelled)
        {
            return;
        }

        if (slot.IsBooked)
        {
            throw new InvalidOperationException("Booked slots cannot be cancelled from the availability page.");
        }

        if (slot.StartTime <= DateTime.Now)
        {
            throw new InvalidOperationException("Past slots cannot be cancelled.");
        }

        slot.IsCancelled = true;
        slot.CancelledAt = DateTime.UtcNow;
        slot.CancelledByUserId = currentUserId;
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ShelterAvailabilitySlotCancelled,
            "ShelterAvailabilitySlot",
            slot.Id.ToString(),
            "Availability slot was cancelled.",
            currentUserId,
            $"ShelterId={slot.ShelterId};StartTime={slot.StartTime:O};EndTime={slot.EndTime:O}");
    }

    public async Task<List<ShelterAvailabilitySlotDto>> GetAvailableSlotsForAdoptionRequestAsync(
        int adoptionRequestId,
        string currentUserId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var request = await context.AdoptionRequests
            .Include(candidate => candidate.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == adoptionRequestId);

        if (request?.Dog is null)
        {
            throw new InvalidOperationException("Adoption request was not found.");
        }

        if (!CanAccessRequestSlots(request, currentUserId))
        {
            throw new InvalidOperationException("You cannot view slots for this adoption request.");
        }

        var now = DateTime.Now;
        var slots = await context.ShelterAvailabilitySlots
            .Where(slot => slot.ShelterId == request.Dog.ShelterId &&
                !slot.IsCancelled &&
                !slot.IsBooked &&
                slot.StartTime > now)
            .OrderBy(slot => slot.StartTime)
            .AsNoTracking()
            .ToListAsync();

        return slots.Select(ToDto).ToList();
    }

    public async Task BookSlotForAdoptionRequestAsync(
        int adoptionRequestId,
        int slotId,
        string currentUserId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var request = await context.AdoptionRequests
            .Include(candidate => candidate.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .FirstOrDefaultAsync(candidate => candidate.Id == adoptionRequestId);

        if (request?.Dog is null)
        {
            throw new InvalidOperationException("Adoption request was not found.");
        }

        if (!CanAccessRequestSlots(request, currentUserId))
        {
            throw new InvalidOperationException("You cannot book a slot for this adoption request.");
        }

        var slot = await context.ShelterAvailabilitySlots
            .FirstOrDefaultAsync(candidate => candidate.Id == slotId);

        ValidateSlotCanBeBooked(slot, request);

        var existingSlots = await context.ShelterAvailabilitySlots
            .Where(candidate => candidate.BookedAdoptionRequestId == request.Id && candidate.Id != slotId)
            .ToListAsync();

        foreach (var existingSlot in existingSlots)
        {
            ReleaseSlot(existingSlot);
        }

        slot!.IsBooked = true;
        slot.BookedAdoptionRequestId = request.Id;
        request.PreferredVisitDateTime = slot.StartTime;
        if (request.VisitStatus == AdoptionVisitStatus.NotScheduled)
        {
            request.VisitStatus = AdoptionVisitStatus.Requested;
        }

        request.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ShelterAvailabilitySlotBooked,
            "ShelterAvailabilitySlot",
            slot.Id.ToString(),
            $"Availability slot was linked to adoption request #{request.Id}.",
            currentUserId,
            $"ShelterId={slot.ShelterId};AdoptionRequestId={request.Id};DogId={request.DogId};StartTime={slot.StartTime:O}");
    }

    private static async Task EnsureShelterOwnershipAsync(
        ApplicationDbContext context,
        int shelterId,
        string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            throw new InvalidOperationException("Current shelter account could not be found.");
        }

        var ownsShelter = await context.Shelters.AnyAsync(shelter =>
            shelter.Id == shelterId &&
            shelter.ApplicationUserId == currentUserId);

        if (!ownsShelter)
        {
            throw new InvalidOperationException("You cannot manage availability for another shelter.");
        }
    }

    private static bool CanAccessRequestSlots(AdoptionRequest request, string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return false;
        }

        return request.AdopterId == currentUserId ||
            request.Dog?.Shelter?.ApplicationUserId == currentUserId;
    }

    private static void ValidateSlotInput(Shelter shelter, DateTime startTime, DateTime endTime, string? notes)
    {
        if (startTime <= DateTime.Now)
        {
            throw new InvalidOperationException("Availability slot must start in the future.");
        }

        if (endTime <= startTime)
        {
            throw new InvalidOperationException("Slot end time must be after start time.");
        }

        var duration = endTime - startTime;
        if (duration < MinimumSlotDuration || duration > MaximumSlotDuration)
        {
            throw new InvalidOperationException("Slot duration must be between 15 minutes and 4 hours.");
        }

        VisitSchedulingHelper.ValidatePreferredVisitTime(shelter, startTime);
        var visitEnd = VisitSchedulingHelper.GetVisitEndTime(shelter);
        if (endTime.Date != startTime.Date || endTime.TimeOfDay > visitEnd)
        {
            throw new InvalidOperationException("Slot must end within the shelter's visiting hours.");
        }

        if (!string.IsNullOrWhiteSpace(notes) && notes.Trim().Length > MaximumNotesLength)
        {
            throw new InvalidOperationException("Slot notes must be 500 characters or fewer.");
        }
    }

    private static void ValidateSlotCanBeBooked(ShelterAvailabilitySlot? slot, AdoptionRequest request)
    {
        if (slot is null)
        {
            throw new InvalidOperationException("Availability slot was not found.");
        }

        if (slot.ShelterId != request.Dog?.ShelterId)
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
    }

    private Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        string currentUserId,
        string? additionalData = null)
    {
        return auditLogService?.LogAsync(
            action,
            entityName,
            entityId,
            description,
            userId: currentUserId,
            additionalData: additionalData) ?? Task.CompletedTask;
    }

    private static void ReleaseSlot(ShelterAvailabilitySlot slot)
    {
        slot.IsBooked = false;
        slot.BookedAdoptionRequestId = null;
    }

    private static ShelterAvailabilitySlotDto ToDto(ShelterAvailabilitySlot slot)
    {
        return new ShelterAvailabilitySlotDto(
            slot.Id,
            slot.ShelterId,
            slot.StartTime,
            slot.EndTime,
            slot.IsBooked,
            slot.BookedAdoptionRequestId,
            slot.BookedAdoptionRequest?.Dog?.Name,
            GetAdopterDisplayName(slot.BookedAdoptionRequest?.Adopter),
            slot.IsCancelled,
            slot.EndTime <= DateTime.Now,
            slot.Notes);
    }

    private static string? GetAdopterDisplayName(ApplicationUser? adopter)
    {
        if (adopter is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(adopter.FullName)
            ? adopter.Email ?? adopter.UserName
            : adopter.FullName;
    }
}
