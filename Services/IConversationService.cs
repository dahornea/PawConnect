namespace PawConnect.Services;

public interface IConversationService
{
    Task<ConversationDto> GetOrCreateForAdoptionRequestAsync(
        int adoptionRequestId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ConversationDto> GetByIdForUserAsync(
        int conversationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessConversationAsync(
        int conversationId,
        string userId,
        CancellationToken cancellationToken = default);
}
