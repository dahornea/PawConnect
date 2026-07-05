using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class NotificationOutboxMessage
{
    public int Id { get; set; }

    public string? RecipientUserId { get; set; }

    public ApplicationUser? RecipientUser { get; set; }

    [StringLength(256)]
    public string? RecipientEmail { get; set; }

    public NotificationEventType NotificationType { get; set; }

    public NotificationChannel Channel { get; set; }

    public NotificationOutboxStatus Status { get; set; } = NotificationOutboxStatus.Pending;

    [Required, StringLength(120)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Body { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Link { get; set; }

    [StringLength(80)]
    public string? RelatedEntityType { get; set; }

    [StringLength(80)]
    public string? RelatedEntityId { get; set; }

    [StringLength(120)]
    public string? CorrelationId { get; set; }

    [StringLength(160)]
    public string? IdempotencyKey { get; set; }

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 5;

    public DateTime? NextAttemptAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public DateTime? SentAt { get; set; }

    [StringLength(500)]
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
