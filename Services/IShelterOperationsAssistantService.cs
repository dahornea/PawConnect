namespace PawConnect.Services;

public interface IShelterOperationsAssistantService
{
    Task<ShelterOperationsBriefDto> GenerateBriefAsync(
        string shelterUserId,
        ShelterOperationsBriefRequest request,
        CancellationToken cancellationToken = default);

    Task<ShelterOperationsBriefDto> GenerateDailyBriefAsync(
        string shelterUserId,
        CancellationToken cancellationToken = default);
}
