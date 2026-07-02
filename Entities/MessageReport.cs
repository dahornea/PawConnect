using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class MessageReport
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    public Message? Message { get; set; }

    [Required]
    public string ReporterUserId { get; set; } = string.Empty;

    public ApplicationUser? ReporterUser { get; set; }

    [Required]
    [StringLength(80)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Details { get; set; }

    public MessageReportStatus Status { get; set; } = MessageReportStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedByAdminId { get; set; }

    public ApplicationUser? ReviewedByAdmin { get; set; }

    [StringLength(1000)]
    public string? AdminNote { get; set; }
}

public enum MessageReportStatus
{
    Pending = 0,
    Reviewed = 1,
    Dismissed = 2,
    ActionTaken = 3
}
