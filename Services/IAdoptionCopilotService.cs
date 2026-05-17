namespace PawConnect.Services;

public interface IAdoptionCopilotService
{
    Task<AdoptionCopilotResponse> AskAsync(
        string adopterUserId,
        string userMessage,
        CancellationToken cancellationToken = default);
}
