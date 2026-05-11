using PawConnect.Entities;

namespace PawConnect.Services;

public interface IShelterRegistrationRequestService
{
    Task<ShelterRegistrationRequest> SubmitRequestAsync(ShelterRegistrationRequest request, string? currentUserId = null);

    Task<List<ShelterRegistrationRequest>> GetAllAsync();

    Task<ShelterRegistrationRequest?> GetByIdAsync(int id);

    Task<ShelterRegistrationRequest> AcceptRequestAsync(int requestId, string adminUserId);

    Task<ShelterRegistrationRequest> RejectRequestAsync(int requestId, string adminUserId);
}
