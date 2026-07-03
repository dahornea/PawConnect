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

    Task<ShelterAdoptionPipelineDto> GetShelterPipelineAsync(string shelterUserId);

    Task<bool> HasPendingRequestAsync(string adopterId, int dogId);

    Task AcceptRequestAsync(int requestId, int shelterId, string? changedByUserId = null);

    Task ConfirmVisitAsync(int requestId, int shelterId, string? changedByUserId = null, int? availabilitySlotId = null);

    Task ConfirmPipelineVisitAsync(int requestId, string shelterUserId, int? availabilitySlotId = null);

    Task MarkAsAdoptedAsync(int requestId, int shelterId, string? changedByUserId = null);

    Task MarkPipelineRequestAsAdoptedAsync(int requestId, string shelterUserId);

    Task RejectRequestAsync(int requestId, int shelterId);

    Task RejectPipelineRequestAsync(int requestId, string shelterUserId);

    Task CancelRequestAsync(int requestId, string adopterId);

    Task UpdateShelterInternalNotesAsync(int requestId, int shelterId, string? notes);
}

public sealed record AdoptionRequestSummary(int Total, int Pending, int Accepted);

public sealed record AdoptionRequestQuestionnaire(
    string ReasonForAdoption,
    int? HoursAlonePerDay,
    string? AdditionalInformation,
    DateTime? PreferredVisitDateTime = null);
