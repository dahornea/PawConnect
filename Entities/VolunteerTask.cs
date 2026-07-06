using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class VolunteerTask
{
    public int Id { get; set; }

    public int ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public int? DogId { get; set; }

    public Dog? Dog { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public int? AssignedVolunteerProfileId { get; set; }

    public VolunteerProfile? AssignedVolunteerProfile { get; set; }

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public VolunteerTaskCategory Category { get; set; } = VolunteerTaskCategory.Other;

    public VolunteerTaskStatus Status { get; set; } = VolunteerTaskStatus.Open;

    public VolunteerTaskPriority Priority { get; set; } = VolunteerTaskPriority.Normal;

    public DateTime ScheduledStartUtc { get; set; }

    public DateTime ScheduledEndUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    [StringLength(250)]
    public string? Location { get; set; }

    [StringLength(500)]
    public string? RequiredSkills { get; set; }

    [StringLength(1000)]
    public string? ShelterNotes { get; set; }

    [StringLength(1000)]
    public string? VolunteerNotes { get; set; }

    [StringLength(1000)]
    public string? CompletionNotes { get; set; }

    public DateTime? AssignedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<VolunteerTaskActivity> Activities { get; set; } = [];
}
