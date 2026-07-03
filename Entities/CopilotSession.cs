using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class CopilotSession
{
    public int Id { get; set; }

    [Required]
    public string AdopterUserId { get; set; } = string.Empty;

    public ApplicationUser? AdopterUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(1000)]
    public string? QueryText { get; set; }

    [StringLength(500)]
    public string? SanitizedQuerySummary { get; set; }

    [StringLength(80)]
    public string? PrimaryIntent { get; set; }

    [StringLength(80)]
    public string? CompatibilityTarget { get; set; }

    [StringLength(80)]
    public string? HomeType { get; set; }

    [StringLength(80)]
    public string? ActivityLevel { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(120)]
    public string? Neighborhood { get; set; }

    public bool UsedAiEnhancement { get; set; }

    public bool UsedSemanticSearch { get; set; }

    public bool UsedToolCalling { get; set; }

    [StringLength(500)]
    public string? FallbackReason { get; set; }

    public string? AppliedConstraintsJson { get; set; }

    public string? ResultDogIdsJson { get; set; }

    public int ResultCount { get; set; }

    public ICollection<CopilotResultFeedback> Feedback { get; set; } = new List<CopilotResultFeedback>();
}
