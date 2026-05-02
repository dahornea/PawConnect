using PawConnect.Entities;

namespace PawConnect.Services;

public interface IAdoptionRequestService
{
    Task<List<AdoptionRequest>> GetAllAsync();

    Task<AdoptionRequest?> GetByIdAsync(int id);

    Task CreateAsync(AdoptionRequest adoptionRequest);

    Task UpdateAsync(AdoptionRequest adoptionRequest);

    Task DeleteAsync(int id);

    Task<List<AdoptionRequest>> GetForAdopterAsync(string userId);

    Task CreateRequestAsync(string adopterId, int dogId, string? message);

    Task<List<AdoptionRequest>> GetRequestsForAdopterAsync(string adopterId);

    Task<List<AdoptionRequest>> GetRequestsForShelterAsync(int shelterId);

    Task<bool> HasPendingRequestAsync(string adopterId, int dogId);

    Task AcceptRequestAsync(int requestId, int shelterId);

    Task RejectRequestAsync(int requestId, int shelterId);

    Task CancelRequestAsync(int requestId, string adopterId);
}
