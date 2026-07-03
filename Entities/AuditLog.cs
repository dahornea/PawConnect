using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PawConnect.Entities;

public class AuditLog
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(80)]
    public string? EntityId { get; set; }

    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(450)]
    public string? UserId { get; set; }

    [StringLength(256)]
    public string? UserEmail { get; set; }

    [StringLength(80)]
    public string? UserRole { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }

    [StringLength(100)]
    public string? CorrelationId { get; set; }

    [StringLength(40)]
    public string Severity { get; set; } = "Information";

    [StringLength(80)]
    public string EventType { get; set; } = "Business";

    [StringLength(2000)]
    public string? AdditionalData { get; set; }

    [StringLength(4000)]
    public string? DetailsJson { get; set; }

    [NotMapped]
    public DateTime TimestampUtc
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    [NotMapped]
    public string EntityType
    {
        get => EntityName;
        set => EntityName = value;
    }

    [NotMapped]
    public string Summary
    {
        get => Description;
        set => Description = value;
    }
}
