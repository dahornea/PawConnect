namespace PawConnect.Services;

public interface IMessageService
{
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        int conversationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<MessageDto> SendMessageAsync(
        int conversationId,
        string senderUserId,
        string body,
        CancellationToken cancellationToken = default);

    Task<MessageDto> SendMessageAsync(
        int conversationId,
        string senderUserId,
        string? body,
        IReadOnlyList<MessageAttachmentUpload> attachments,
        CancellationToken cancellationToken = default);

    Task<MessageDto> EditMessageAsync(
        int messageId,
        string newBody,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<MessageAttachmentFile?> GetAttachmentFileAsync(
        int attachmentId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<MessageReactionUpdateDto> AddReactionAsync(
        int messageId,
        string reactionType,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<MessageReactionUpdateDto> RemoveReactionAsync(
        int messageId,
        string reactionType,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<MessageReactionUpdateDto> ToggleReactionAsync(
        int messageId,
        string reactionType,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageReactionSummaryDto>> GetReactionsForMessageAsync(
        int messageId,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task MarkConversationAsReadAsync(
        int conversationId,
        string userId,
        CancellationToken cancellationToken = default);
}
