using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class VolunteerProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required, StringLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(40)]
    public string? PhoneNumber { get; set; }

    public int? PreferredShelterId { get; set; }

    public Shelter? PreferredShelter { get; set; }

    [StringLength(1000)]
    public string? Skills { get; set; }

    [StringLength(1000)]
    public string? AvailabilityNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<VolunteerTask> AssignedTasks { get; set; } = [];
}
