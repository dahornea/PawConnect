using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class NotificationDeliveryLog
{
    public int Id { get; set; }

    public int? NotificationId { get; set; }

    public Notification? Notification { get; set; }

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public NotificationEventType NotificationType { get; set; }

    public NotificationChannel Channel { get; set; }

    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;

    [StringLength(256)]
    public string? Recipient { get; set; }

    [StringLength(200)]
    public string? Subject { get; set; }

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    [StringLength(120)]
    public string? ProviderMessageId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public DateTime? FailedAt { get; set; }

    public int RetryCount { get; set; }

    [StringLength(80)]
    public string? RelatedEntityType { get; set; }

    [StringLength(80)]
    public string? RelatedEntityId { get; set; }
}
