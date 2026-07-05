using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record NotificationDeliveryLogCreateRequest(
    NotificationEventType NotificationType,
    NotificationChannel Channel,
    NotificationDeliveryStatus Status,
    int? NotificationId = null,
    string? UserId = null,
    string? Recipient = null,
    string? Subject = null,
    string? ErrorMessage = null,
    string? ProviderMessageId = null,
    DateTime? SentAt = null,
    DateTime? FailedAt = null,
    int RetryCount = 0,
    string? RelatedEntityType = null,
    string? RelatedEntityId = null);

public sealed record NotificationDeliveryLogFilter(
    NotificationDeliveryStatus? Status = null,
    NotificationChannel? Channel = null,
    NotificationEventType? NotificationType = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null);

public sealed record NotificationDeliveryLogDto(
    int Id,
    int? NotificationId,
    string? UserId,
    string UserDisplayName,
    NotificationEventType NotificationType,
    NotificationChannel Channel,
    NotificationDeliveryStatus Status,
    string? Recipient,
    string? Subject,
    string? ErrorMessage,
    string? ProviderMessageId,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? FailedAt,
    int RetryCount,
    string? RelatedEntityType,
    string? RelatedEntityId);

public sealed record NotificationDeliverySummaryDto(
    int Total,
    int Sent,
    int Failed,
    int Pending,
    int Skipped,
    int DisabledByPreference);
