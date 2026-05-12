using System.ComponentModel.DataAnnotations;

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

    [StringLength(2000)]
    public string? AdditionalData { get; set; }
}
