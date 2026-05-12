using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class ReportHistory
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string ReportType { get; set; } = string.Empty;

    [StringLength(256)]
    public string? RecipientEmail { get; set; }

    [StringLength(200)]
    public string? Subject { get; set; }

    [StringLength(180)]
    public string? FileName { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public bool WasSuccessful { get; set; }

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    [Required, StringLength(40)]
    public string TriggeredBy { get; set; } = string.Empty;

    [StringLength(450)]
    public string? TriggeredByUserId { get; set; }

    [StringLength(256)]
    public string? TriggeredByUserEmail { get; set; }

    public int? ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    [StringLength(450)]
    public string? AdminUserId { get; set; }

    [StringLength(80)]
    public string? RelatedEntityName { get; set; }

    [StringLength(80)]
    public string? RelatedEntityId { get; set; }
}
