using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class FosterPlacement
{
    public int Id { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    public int ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public int FosterCaregiverProfileId { get; set; }

    public FosterCaregiverProfile? FosterCaregiverProfile { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public string? ApprovedByUserId { get; set; }

    public ApplicationUser? ApprovedByUser { get; set; }

    public string? EndedByUserId { get; set; }

    public ApplicationUser? EndedByUser { get; set; }

    public FosterPlacementStatus Status { get; set; } = FosterPlacementStatus.Pending;

    public FosterPlacementPriority Priority { get; set; } = FosterPlacementPriority.Normal;

    public FosterPlacementReason Reason { get; set; } = FosterPlacementReason.Other;

    public DateTime StartDateUtc { get; set; }

    public DateTime? PlannedEndDateUtc { get; set; }

    public DateTime? ActualEndDateUtc { get; set; }

    [StringLength(1000)]
    public string? CareInstructions { get; set; }

    [StringLength(1000)]
    public string? MedicalNotesSummary { get; set; }

    [StringLength(1000)]
    public string? ShelterNotes { get; set; }

    [StringLength(1000)]
    public string? FosterNotes { get; set; }

    [StringLength(1000)]
    public string? CompletionNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<FosterPlacementActivity> Activities { get; set; } = new List<FosterPlacementActivity>();
}
