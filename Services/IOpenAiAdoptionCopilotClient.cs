namespace PawConnect.Services;

public interface IOpenAiAdoptionCopilotClient
{
    Task<OpenAiAdoptionCopilotResponse> AskWithToolsAsync(
        AdoptionCopilotToolOpenAiRequest request,
        OpenAiCopilotToolExecutor executeToolAsync,
        CancellationToken cancellationToken = default);
}
