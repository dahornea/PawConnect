namespace PawConnect.Services;

public interface IShelterAvailabilityService
{
    Task<List<ShelterAvailabilitySlotDto>> GetShelterSlotsAsync(
        int shelterId,
        DateTime from,
        DateTime to,
        string currentUserId);

    Task<ShelterAvailabilitySlotDto> CreateSlotAsync(
        CreateShelterAvailabilitySlotRequest request,
        string currentUserId);

    Task CancelSlotAsync(int slotId, string currentUserId);

    Task<List<ShelterAvailabilitySlotDto>> GetAvailableSlotsForAdoptionRequestAsync(
        int adoptionRequestId,
        string currentUserId);

    Task BookSlotForAdoptionRequestAsync(
        int adoptionRequestId,
        int slotId,
        string currentUserId);
}
