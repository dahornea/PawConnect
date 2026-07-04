namespace PawConnect.Services;

public interface IOpenAiShelterOperationsAssistantClient
{
    Task<OpenAiShelterOperationsAssistantResponse> GenerateBriefAsync(
        ShelterOperationsBriefInputDto input,
        CancellationToken cancellationToken = default);
}
