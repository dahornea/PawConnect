using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record NotificationOutboxCreateRequest(
    NotificationEventType NotificationType,
    NotificationChannel Channel,
    string Subject,
    string Body,
    string? RecipientUserId = null,
    string? RecipientEmail = null,
    string? Link = null,
    string? RelatedEntityType = null,
    string? RelatedEntityId = null,
    string? CorrelationId = null,
    string? IdempotencyKey = null,
    int MaxAttempts = 5,
    DateTime? NextAttemptAt = null);

public sealed record NotificationOutboxFilter(
    NotificationOutboxStatus? Status = null,
    NotificationChannel? Channel = null,
    NotificationEventType? NotificationType = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null);

public sealed record NotificationOutboxMessageDto(
    int Id,
    string? RecipientUserId,
    string RecipientDisplayName,
    string? RecipientEmail,
    NotificationEventType NotificationType,
    NotificationChannel Channel,
    NotificationOutboxStatus Status,
    string Subject,
    string Body,
    string? Link,
    string? RelatedEntityType,
    string? RelatedEntityId,
    string? CorrelationId,
    string? IdempotencyKey,
    int AttemptCount,
    int MaxAttempts,
    DateTime? NextAttemptAt,
    DateTime? LastAttemptAt,
    DateTime? SentAt,
    string? LastError,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record NotificationOutboxSummaryDto(
    int Total,
    int Pending,
    int Processing,
    int Sent,
    int Failed,
    int DeadLetter,
    int Cancelled);

public sealed record NotificationOutboxProcessResult(
    int Processed,
    int Sent,
    int Failed,
    int DeadLettered,
    int Skipped);
