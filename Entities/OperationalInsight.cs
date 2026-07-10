using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class OperationalInsight
{
    public int Id { get; set; }

    [Required, StringLength(240)]
    public string Fingerprint { get; set; } = string.Empty;

    public IntelligenceAudienceType AudienceType { get; set; }

    [StringLength(450)]
    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public int? ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public IntelligenceCategory Category { get; set; }

    [Required, StringLength(80)]
    public string InsightType { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string SourceModule { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string EntityType { get; set; } = string.Empty;

    [StringLength(80)]
    public string? EntityId { get; set; }

    [StringLength(160)]
    public string? EntityDisplayName { get; set; }

    [Required, StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Summary { get; set; } = string.Empty;

    public IntelligenceSeverity Severity { get; set; }

    [Range(0, 100)]
    public int PriorityScore { get; set; }

    [Required, StringLength(40)]
    public string ConfidenceLabel { get; set; } = "High";

    [Required, StringLength(2000)]
    public string Explanation { get; set; } = string.Empty;

    [Required, StringLength(8000)]
    public string EvidenceJson { get; set; } = "[]";

    [Required, StringLength(4000)]
    public string ScoreBreakdownJson { get; set; } = "[]";

    [Required, StringLength(4000)]
    public string RecommendedActionsJson { get; set; } = "[]";

    public IntelligenceInsightStatus Status { get; set; } = IntelligenceInsightStatus.Active;

    public DateTime FirstDetectedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastDetectedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastEvaluatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? AcknowledgedAtUtc { get; set; }

    [StringLength(450)]
    public string? AcknowledgedByUserId { get; set; }

    public DateTime? SnoozedUntilUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    [StringLength(500)]
    public string? ResolutionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

