namespace PawConnect.Services;

public sealed record ConversationDto(
    int Id,
    int AdoptionRequestId,
    string DogName,
    string ShelterName,
    string AdopterName,
    string OtherParticipantName,
    DateTime CreatedAt,
    DateTime? LastMessageAt);

public sealed record MessageDto(
    int Id,
    int ConversationId,
    string SenderDisplayName,
    string SenderRole,
    string Body,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsEdited,
    bool IsOwnMessage,
    bool IsRead,
    string SenderUserId,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    IReadOnlyList<MessageReactionSummaryDto> Reactions);

public sealed record MessageAttachmentDto(
    int Id,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string DownloadUrl,
    bool IsImage);

public sealed record MessageAttachmentUpload(
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    Stream Content);

public sealed record MessageAttachmentFile(
    Stream Content,
    string OriginalFileName,
    string ContentType);

public sealed record MessageReactionSummaryDto(
    string ReactionType,
    string DisplayText,
    int Count,
    bool ReactedByCurrentUser);

public sealed record MessageReactionUpdateDto(
    int ConversationId,
    int MessageId,
    string ChangedByUserId,
    string ReactionType,
    bool Removed,
    IReadOnlyList<MessageReactionSummaryDto> Reactions);

public sealed record MessageTypingIndicatorDto(
    int ConversationId,
    string UserId,
    string SenderRole,
    bool IsTyping);
