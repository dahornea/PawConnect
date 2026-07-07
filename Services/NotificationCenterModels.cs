using PawConnect.Entities;

namespace PawConnect.Services;

public enum NotificationReadState
{
    All = 0,
    Unread = 1,
    Read = 2
}

public sealed record NotificationCenterQuery(
    NotificationCategory? Category = null,
    NotificationReadState ReadState = NotificationReadState.All,
    string? SearchTerm = null,
    int Count = 300);

public sealed record NotificationCenterItemDto(
    int Id,
    string Title,
    string Message,
    NotificationCategory Category,
    NotificationType Type,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    string? RelatedEntityType,
    string? RelatedEntityId,
    string? RelatedEntityDisplayName,
    string? RelatedUrl,
    string CategoryLabel,
    string SeverityLabel,
    string Icon,
    string ActionLabel,
    string MetadataSummary,
    string TimeGroup,
    string RelativeTime);

public sealed record NotificationCenterGroupDto(
    string Label,
    IReadOnlyList<NotificationCenterItemDto> Items);

public sealed record NotificationCenterResultDto(
    IReadOnlyList<NotificationCenterGroupDto> Groups,
    int TotalCount,
    int UnreadCount,
    IReadOnlyList<NotificationCategory> AvailableCategories);
