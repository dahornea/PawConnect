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

    Task CreateRequestAsync(string adopterId, int dogId, AdoptionRequestQuestionnaire questionnaire);

    Task<List<AdoptionRequest>> GetRequestsForAdopterAsync(string adopterId);

    Task<AdoptionRequestSummary> GetAdoptionRequestSummaryForUserAsync(string adopterId);

    Task<List<AdoptionRequest>> GetRecentRequestsForAdopterAsync(string adopterId, int count);

    Task<List<AdoptionRequest>> GetRequestsForShelterAsync(int shelterId);

    Task<bool> HasPendingRequestAsync(string adopterId, int dogId);

    Task AcceptRequestAsync(int requestId, int shelterId);

    Task RejectRequestAsync(int requestId, int shelterId);

    Task CancelRequestAsync(int requestId, string adopterId);
}

public sealed record AdoptionRequestSummary(int Total, int Pending, int Accepted);

public sealed record AdoptionRequestQuestionnaire(
    string ReasonForAdoption,
    int? HoursAlonePerDay,
    string? AdditionalInformation);
