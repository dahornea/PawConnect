namespace PawConnect.Services;

public interface ICopilotHistoryService
{
    Task<int> SaveSessionAsync(
        string adopterUserId,
        string queryText,
        AdoptionCopilotResponse response,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CopilotHistoryItemDto>> GetRecentSessionsAsync(
        string adopterUserId,
        int count = 10,
        CancellationToken cancellationToken = default);

    Task<CopilotSessionDto?> GetSessionAsync(
        int sessionId,
        string adopterUserId,
        CancellationToken cancellationToken = default);
}
